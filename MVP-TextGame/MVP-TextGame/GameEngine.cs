using MVP_TextGame.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    internal class GameEngine
    {
        private readonly GameMap _map;
        private readonly List<Player> _activePlayers;

        public GameEngine(GameMap map, List<Player> activePlayers)
        {
            _map = map;
            _activePlayers = activePlayers;
        }

        public async Task ProcessCommand(Player player, string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return;
            }

            if (player.TalkOptions != null)
            {
                if (int.TryParse(input, out int index))
                {
                    index -= 1;

                    if (index >= 0 && index < player.TalkOptions.Count)
                    {
                        var npc = player.TalkOptions[index];
                        GameLogger.Log($"[DIALOGUE]: {player.Name} promluvil s NPC {npc.Name}.");
                        await player.Writer.WriteLineAsync($"{npc.Name} says: {npc.Dialogue}");

                        var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == player.CurrentRoom && p != player);
                        foreach (var p in otherPlayers)
                        {
                            await p.Writer.WriteLineAsync($"{player.Name} is talking to {npc.Name}. {npc.Name} says: {npc.Dialogue}");
                        }
                    }
                    else
                    {
                        await player.Writer.WriteLineAsync("Invalid selection.");
                    }
                }
                else
                {
                    await player.Writer.WriteLineAsync("Please enter a number.");
                }

                player.TalkOptions = null;
                return;
            }

            await HandleQuest(player, input);

            string[] parts = input.Split(' ', 2);
            string command = parts[0].ToLower();
            string argument = parts.Length > 1 ? parts[1] : "";

            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            GameLogger.Log($"[ACTION]: Hráč {player.Name} použil příkaz: {command} {argument}");
            Console.WriteLine($"Received command from {player.Name}: {command}");

            switch (command)
            {
                case "exit":
                    await player.Writer.WriteLineAsync("Exiting the game. Goodbye!");
                    break;

                case "help":
                    await player.Writer.WriteLineAsync("Available commands:");
                    await player.Writer.WriteLineAsync("help - Show this help message");
                    await player.Writer.WriteLineAsync("whisper <message> - Send message to other player");
                    await player.Writer.WriteLineAsync("exit - Exit the game");
                    await player.Writer.WriteLineAsync("go - Move to an available room");
                    await player.Writer.WriteLineAsync("talk - Starts a dialog with an NPC");
                    await player.Writer.WriteLineAsync("attack - Start attacking an enemy");
                    await player.Writer.WriteLineAsync("inventory - show inventory");
                    await player.Writer.WriteLineAsync("equip - equip an item");
                    await player.Writer.WriteLineAsync("stats - show your current statistics");
                    break;

                case "whisper":
                    await HandleWhisper(player, argument);
                    break;

                case "go":
                    await HandleGo(player, argument);
                    break;

                case "talk":
                    await HandleTalk(player);
                    break;

                case "attack":
                    await HandleAttack(player);
                    break;
                case "stats":
                    await ShowStats(player);
                    break;
                case "inventory":
                    await ShowInventory(player);
                    break;

                case "equip":
                    await HandleEquip(player, argument);
                    break;

                default:
                    await player.Writer.WriteLineAsync($"Unknown command: {command}");
                    break;
            }
        }
        public async Task HelpOnPlayerDeath(Player player)
        {
            player.Deaths++;
            player.CurrentHealth = player.MaxHealth;

            GameLogger.Log($"[DEATH]: Hráč {player.Name} zemřel! (Počet smrtí: {player.Deaths})");

            await player.Writer.WriteLineAsync($"YOU HAVE DIED!!");
            await player.Writer.WriteLineAsync("You have been respawned at the start of the mine.");

            if (player.Deaths == 3)
            {
                await player.Writer.WriteLineAsync("[HELP]: Try to find better weapons and do sidequests.");
            }
        }

        public async Task HandleWhisper(Player sender, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                await sender.Writer.WriteLineAsync("Usage: whisper <message>");
                return;
            }

            Player receiver = null;

            lock (_activePlayers)
            {
                receiver = _activePlayers.FirstOrDefault(p => p != sender);
            }

            if (receiver == null)
            {
                await sender.Writer.WriteLineAsync("No other player connected.");
                return;
            }

            GameLogger.Log($"[CHAT]: {sender.Name} whispered to {receiver.Name}");
            await receiver.Writer.WriteLineAsync($"[Whisper from {sender.Name}]: {message}");
        }

        public async Task HandleGo(Player player, string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                await player.Writer.WriteLineAsync("Usage: go <room name>");
                return;
            }

            var currentRoom = player.CurrentRoom;

            if (currentRoom.Connections == null || currentRoom.Connections.Count == 0)
            {
                await player.Writer.WriteLineAsync("No available paths.");
                return;
            }

            var target = currentRoom.Connections
                .FirstOrDefault(x => x.Value.Equals(roomName, StringComparison.OrdinalIgnoreCase));

            if (target.Value == null)
            {
                await player.Writer.WriteLineAsync("You can't go there.");
                await ShowAvailableRooms(player);
                return;
            }

            var nextRoom = _map.GetRoom(target.Value);

            if (nextRoom == null) return;

            if (nextRoom.Blocked)
            {
                await player.Writer.WriteLineAsync($"The path is blocked by {nextRoom.BlockedBy}. You need to clear it first.");
                return;
            }

            if (nextRoom.Type == "final" && !player.HasKey)
            {
                await player.Writer.WriteLineAsync("The door is locked. You need a key.");
                return;
            }

            await Broadcast($"{player.Name} left the room.", currentRoom);

            GameLogger.Log($"[MOVEMENT]: {player.Name} se přesunul z {currentRoom.Name} do {nextRoom.Name}");
            await EnterRoom(player, nextRoom);
        }

        public async Task ShowRoom(Player player)
        {
            var room = player.CurrentRoom;

            await player.Writer.WriteLineAsync($"\n=== {room.Name} ===");
            await player.Writer.WriteLineAsync(room.Description);

            await ShowAvailableRooms(player);

            if (room.Npcs != null && room.Npcs.Count > 0)
            {
                await player.Writer.WriteLineAsync("NPCs here:");

                foreach (var npc in room.Npcs)
                {
                    await player.Writer.WriteLineAsync($"- {npc.Name}");
                }
            }
            else
            {
                await player.Writer.WriteLineAsync("No NPCs here.");
            }
        }
        public async Task ShowAvailableRooms(Player player)
        {
            var room = player.CurrentRoom;

            if (room.Connections == null || room.Connections.Count == 0)
            {
                await player.Writer.WriteLineAsync("No exits.");
                return;
            }

            await player.Writer.WriteLineAsync("Available rooms:");

            foreach (var connection in room.Connections.Values)
            {
                await player.Writer.WriteLineAsync($"- {connection}");
            }
        }
        public async Task EnterRoom(Player player, Room room)
        {
            player.CurrentRoom = room;

            await player.Writer.WriteLineAsync($"\n=== {room.Name} ===");
            await player.Writer.WriteLineAsync(room.Description);

            var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
            foreach (var p in otherPlayers)
            {
                await p.Writer.WriteLineAsync($"\n{player.Name} entered the room.");
            }

            if (room.Boss != null && room.Boss.HP > 0)
            {
                player.InCombat = true;
                await player.Writer.WriteLineAsync($"A boss blocks your path: {room.Boss.Name} (HP: {room.Boss.HP})");
                return;
            }

            var enemy = room.Npcs?.FirstOrDefault(n => n.IsEnemy && n.HP > 0);
            if (enemy != null)
            {
                player.InCombat = true;
                await player.Writer.WriteLineAsync($"Watch out! An enemy is here: {enemy.Name} (HP: {enemy.HP})");
            }

            await ShowRoom(player);
        }

        public async Task HandleTalk(Player player)
        {
            var room = player.CurrentRoom;

            if (room.Npcs == null || room.Npcs.Count == 0)
            {
                await player.Writer.WriteLineAsync("There is no one to talk to here.");
                return;
            }

            player.TalkOptions = room.Npcs;

            await player.Writer.WriteLineAsync("Who do you want to talk to?");

            for (int i = 0; i < room.Npcs.Count; i++)
            {
                await player.Writer.WriteLineAsync($"{i + 1}. {room.Npcs[i].Name}");
            }

            await player.Writer.WriteLineAsync("Type number:");
        }

        public async Task HandleAttack(Player player)
        {
            var room = player.CurrentRoom;

            Npc target = room.Boss;
            if (target == null || target.HP <= 0)
            {
                target = room.Npcs?.FirstOrDefault(n => n.IsEnemy && n.HP > 0);
            }

            if (target == null)
            {
                await player.Writer.WriteLineAsync("There is nothing to attack here.");
                player.InCombat = false;
                return;
            }

            Random rnd = new Random();
            int playerDamage = player.AttackStrength + rnd.Next(-2, 5);
            int enemyDamage = target.Attack + rnd.Next(-2, 3);

            bool enemyDefeated = false;

            lock (target)
            {
                if (target.HP <= 0)
                {
                    player.Writer.WriteLineAsync($"{target.Name} is already dead!").Wait();
                    return;
                }

                target.HP -= playerDamage;
                int remainingHP = Math.Max(0, target.HP);

                GameLogger.Log($"[COMBAT]: {player.Name} zasáhl {target.Name} za {playerDamage} HP. Zůstává {remainingHP} HP.");

                Broadcast($"{player.Name} hits {target.Name} for {playerDamage} damage! (Enemy HP: {remainingHP})", room).Wait();

                if (target.HP <= 0)
                {
                    enemyDefeated = true;
                }
            }

            if (enemyDefeated)
            {
                GameLogger.Log($"[COMBAT]: {target.Name} byl poražen v {room.Name}!");
                await Broadcast($"\n>>> {target.Name} was defeated! <<<", room);

                foreach (var p in _activePlayers.Where(p => p.CurrentRoom == room))
                {
                    p.InCombat = false;
                }

                if (target.Name.Contains("MiniBoss1")) player.Quests["Q1"] = true;
                if (target.Name.Contains("MiniBoss3")) player.HasKey = true;
                if (target.Name.Contains("FinalBoss")) await Broadcast("\nYOU HAVE DEFEATED THE KING ORG! YOU WON THE GAME!");

                await GiveLoot(player);
                return;
            }

            player.CurrentHealth -= enemyDamage;
            await player.Writer.WriteLineAsync($"{target.Name} hits YOU for {enemyDamage} damage. (Your HP: {player.CurrentHealth})");

            if (player.CurrentHealth <= 0)
            {
                await Broadcast($"{player.Name} was killed by {target.Name}!", room);
                await HelpOnPlayerDeath(player);
            }
        }
        public async Task GiveLoot(Player player)
        {
            Random rnd = new Random();
            var room = player.CurrentRoom;

            if (rnd.Next(0, 100) < 50)
            {
                player.Inventory.Add("Healing Potion");
                await player.Writer.WriteLineAsync("You found a Healing Potion!");
                GameLogger.Log($"[LOOT]: {player.Name} našel Healing Potion.");
            }

            if (rnd.Next(0, 100) < 30)
            {
                player.Inventory.Add("Iron Weapon");
                player.AttackStrength = 8;
                await player.Writer.WriteLineAsync("You found an Iron Weapon!");
                GameLogger.Log($"[LOOT]: {player.Name} našel Iron Weapon.");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"{player.Name} found a weapon in the loot!");
                }
            }

            if (rnd.Next(0, 100) < 20)
            {
                player.HasKey = true;
                await player.Writer.WriteLineAsync("You found THE KEY!");
                GameLogger.Log($"[LOOT]: {player.Name} NAŠEL KLÍČ!");
                await Broadcast($"\n>>> {player.Name} FOUND THE VAULT KEY! <<<", room);
            }
        }
        public async Task ShowInventory(Player player)
        {
            await player.Writer.WriteLineAsync("=== INVENTORY ===");

            if (player.Inventory.Count == 0)
            {
                await player.Writer.WriteLineAsync("Empty");
                return;
            }

            for (int i = 0; i < player.Inventory.Count; i++)
            {
                await player.Writer.WriteLineAsync($"{i + 1}. {player.Inventory[i]}");
            }

            await player.Writer.WriteLineAsync("Use: equip <number>");
        }

        public async Task HandleEquip(Player player, string arg)
        {
            if (!int.TryParse(arg, out int index))
            {
                await player.Writer.WriteLineAsync("Invalid item number.");
                return;
            }

            index--;

            if (index < 0 || index >= player.Inventory.Count)
            {
                await player.Writer.WriteLineAsync("Out of range.");
                return;
            }

            string item = player.Inventory[index];
            var room = player.CurrentRoom;


            if (item == "Healing Potion")
            {
                player.CurrentHealth = player.MaxHealth;
                player.Inventory.RemoveAt(index);

                GameLogger.Log($"[ITEM]: {player.Name} použil Healing Potion.");

                await player.Writer.WriteLineAsync("You drank a Healing Potion. Your HP is fully restored!");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"{player.Name} drank a Healing Potion.");
                }
                return;
            }

            if (item.Contains("Weapon"))
            {
                player.EquippedWeapon = item;
                player.AttackStrength = 8;
                GameLogger.Log($"[ITEM]: {player.Name} si vybavil {item}.");
                await player.Writer.WriteLineAsync($"Equipped weapon: {item}");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"{player.Name} equipped an {item}.");
                }
            }
            else if (item.Contains("Armor"))
            {
                GameLogger.Log($"[ITEM]: {player.Name} si oblékl {item}.");
                await player.Writer.WriteLineAsync($"Equipped armor: {item}");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"{player.Name} put on {item}.");
                }
            }
            else
            {
                await player.Writer.WriteLineAsync($"Used item: {item}");
            }

            if (item == "Iron Pickaxe")
            {
                var shaft1 = _map.GetRoom("Shaft1");
                if (shaft1 != null && shaft1.Blocked)
                {
                    shaft1.Blocked = false;
                    GameLogger.Log($"[WORLD]: {player.Name} použil Iron Pickaxe a otevřel Shaft1.");
                    await player.Writer.WriteLineAsync("You used the Iron Pickaxe to clear the rubble!");
                    await Broadcast($"\n>>> {player.Name} cleared the path to Shaft1! <<<");
                }
                else
                {
                    await player.Writer.WriteLineAsync("There is nothing to mine here.");
                }
            }
        }
        public async Task HandleQuest(Player player, string input)
        {
            var room = player.CurrentRoom;

            if (room == null || room.Npcs == null)
                return;

            foreach (var npc in room.Npcs)
            {
                if (npc.Name.Contains("Miner") && !player.Quests.ContainsKey("Q1"))
                {
                    if (input.ToLower().Contains("done"))
                    {
                        player.Quests["Q1"] = true;
                        player.Inventory.Add("Iron Pickaxe");
                        GameLogger.Log($"[QUEST]: {player.Name} splnil quest pro Miner (Q1).");
                        await player.Writer.WriteLineAsync("Quest 1 completed! You got an Iron Pickaxe.");
                        await Broadcast($"{player.Name} completed a quest for {npc.Name} and got a reward!", room);
                        return;
                    }
                }

                if (npc.Name.Contains("RiddleMaster") && !player.Quests.ContainsKey("Q3"))
                {
                    if (input.ToLower().Contains("growth") || input.ToLower().Contains("age"))
                    {
                        player.Quests["Q3"] = true;
                        GameLogger.Log($"[QUEST]: {player.Name} uhádl hádanku pro RiddleMaster (Q3).");
                        await player.Writer.WriteLineAsync("Correct! Riddle solved.");
                        await Broadcast($"{player.Name} correctly answered the RiddleMaster's riddle!", room);
                        return;
                    }
                }
            }
        }

        public async Task ShowStats(Player player)
        {
            await player.Writer.WriteLineAsync("\n=== PLAYER STATS ===");
            await player.Writer.WriteLineAsync($"Name:     {player.Name}");
            await player.Writer.WriteLineAsync($"HP:       {player.CurrentHealth} / {player.MaxHealth}");
            await player.Writer.WriteLineAsync($"Attack:   {player.AttackStrength}");
            await player.Writer.WriteLineAsync($"Weapon:   {player.EquippedWeapon}");
            await player.Writer.WriteLineAsync($"Deaths:   {player.Deaths}");
            await player.Writer.WriteLineAsync($"Quests:   {player.Quests.Count} completed");
            await player.Writer.WriteLineAsync($"Has Key:  {(player.HasKey ? "Yes" : "No")}");
            await player.Writer.WriteLineAsync("====================\n");
        }

        public async Task Broadcast(string message, Room room = null)
        {
            var targets = room == null
                ? _activePlayers
                : _activePlayers.Where(p => p.CurrentRoom == room).ToList();

            foreach (var p in targets)
            {
                await p.Writer.WriteLineAsync(message);
            }
        }
    }
}
