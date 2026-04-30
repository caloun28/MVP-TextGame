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
                        GameLogger.Log($"[DIALOGUE]: {player.Name} has talked to npc {npc.Name}.");
                        await player.Writer.WriteLineAsync($"\x1b[38;5;208m{npc.Name} \x1b[38;5;118msays: \x1b[38;2;255;105;180m{npc.Dialogue}\x1b[0m");

                        var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == player.CurrentRoom && p != player);
                        foreach (var p in otherPlayers)
                        {
                            await p.Writer.WriteLineAsync($"\x1b[38;5;226m{player.Name} \x1b[38;5;118mis talking to \x1b[38;5;208m{npc.Name}. \x1b[38;5;208m{npc.Name} \x1b[38;5;118msays: \x1b[38;2;255;105;180m{npc.Dialogue}\x1b[0m");
                        }
                    }
                    else
                    {
                        await player.Writer.WriteLineAsync("\x1b[32mInvalid selection.\x1b[0m");
                    }
                }
                else
                {
                    await player.Writer.WriteLineAsync("\x1b[32mPlease enter a number.\x1b[0m");
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

            GameLogger.Log($"[ACTION]: Player {player.Name} has used a command: {command} {argument}");
            Console.WriteLine($"\u001b[38;5;118mReceived command from \u001b[38;5;226m{player.Name}: {command}\x1b[0m");

            switch (command)
            {
                case "exit":
                    await player.Writer.WriteLineAsync("\x1b[32mExiting the game. Goodbye!\x1b[0m");
                    break;

                case "help":
                    await player.Writer.WriteLineAsync("\x1b[32mAvailable commands:\x1b[0m");
                    await player.Writer.WriteLineAsync("help - \x1b[32mShow this help message\x1b[0m");
                    await player.Writer.WriteLineAsync("whisper <message> - \x1b[32mSend message to other player\x1b[0m");
                    await player.Writer.WriteLineAsync("exit - \x1b[32mExit the game\x1b[0m");
                    await player.Writer.WriteLineAsync("go - \x1b[32mMove to an available room\x1b[0m");
                    await player.Writer.WriteLineAsync("talk - \x1b[32mStarts a dialog with an NPC\x1b[0m");
                    await player.Writer.WriteLineAsync("attack - \x1b[32mStart attacking an enemy\x1b[0m");
                    await player.Writer.WriteLineAsync("inventory - \x1b[32mShow inventory\x1b[0m");
                    await player.Writer.WriteLineAsync("equip - \x1b[32mEquip an item\x1b[0m");
                    await player.Writer.WriteLineAsync("stats - \x1b[32mShow your current statistics\x1b[0m");
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

            GameLogger.Log($"[DEATH]: Player {player.Name} has died! (Death count: {player.Deaths})");

            await player.Writer.WriteLineAsync($"\u001b[38;5;118mYOU HAVE DIED!!\x1b[0m");
            await player.Writer.WriteLineAsync("\u001b[38;5;118mYou have been respawned at the start of the mine.\u001b[0m");

            if (player.Deaths == 3)
            {
                await player.Writer.WriteLineAsync("[HELP]: \u001b[32mTry to find better weapons and do sidequests.\u001b[0m");
            }
        }

        public async Task HandleWhisper(Player sender, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                await sender.Writer.WriteLineAsync("\u001b[32mUsage: whisper <message>\u001b[0m");
                return;
            }

            Player receiver = null;

            lock (_activePlayers)
            {
                receiver = _activePlayers.FirstOrDefault(p => p != sender);
            }

            if (receiver == null)
            {
                await sender.Writer.WriteLineAsync("\u001b[32mNo other player connected.\u001b[0m");
                return;
            }

            GameLogger.Log($"[CHAT]: {sender.Name} whispered to {receiver.Name}\u001b[0m");
            await receiver.Writer.WriteLineAsync($"\u001b[32m[Whisper from \u001b[38;5;226m{sender.Name}]: \u001b[0m{message}\u001b[0m");
        }

        public async Task HandleGo(Player player, string roomName)
        {
            if (string.IsNullOrWhiteSpace(roomName))
            {
                await player.Writer.WriteLineAsync("\u001b[32mUsage: go <room name>\u001b[0m");
                return;
            }

            var currentRoom = player.CurrentRoom;

            if (currentRoom.Connections == null || currentRoom.Connections.Count == 0)
            {
                await player.Writer.WriteLineAsync("\u001b[32mNo available paths.\u001b[0m");
                return;
            }

            var target = currentRoom.Connections
                .FirstOrDefault(x => x.Value.Equals(roomName, StringComparison.OrdinalIgnoreCase));

            if (target.Value == null)
            {
                await player.Writer.WriteLineAsync("\u001b[32mYou can't go there.\u001b[0m");
                await ShowAvailableRooms(player);
                return;
            }

            var nextRoom = _map.GetRoom(target.Value);

            if (nextRoom == null) return;

            if (nextRoom.Blocked)
            {
                await player.Writer.WriteLineAsync($"\u001b[38;5;118mThe path is blocked by \u001b[0m{nextRoom.BlockedBy}. \u001b[38;5;118mYou have to figure out how to get in.\u001b[0m");
                return;
            }

            if (nextRoom.Type == "final" && !player.HasKey)
            {
                await player.Writer.WriteLineAsync("\u001b[38;5;118mThe door is locked. You need a key.\u001b[0m");
                return;
            }

            await Broadcast($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mleft the room.\u001b[0m", currentRoom);

            GameLogger.Log($"[MOVEMENT]: {player.Name} has moved from {currentRoom.Name} to {nextRoom.Name}");
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
                await player.Writer.WriteLineAsync("\u001b[32mNPCs here:\u001b[0m");

                foreach (var npc in room.Npcs)
                {
                    await player.Writer.WriteLineAsync($"- {npc.Name}");
                }
            }
            else
            {
                await player.Writer.WriteLineAsync("\u001b[32mNo NPCs here.\u001b[0m");
            }
        }
        public async Task ShowAvailableRooms(Player player)
        {
            var room = player.CurrentRoom;

            if (room.Connections == null || room.Connections.Count == 0)
            {
                await player.Writer.WriteLineAsync("\u001b[32mNo exits.\u001b[0m");
                return;
            }

            await player.Writer.WriteLineAsync("\u001b[32mAvailable rooms:\u001b[0m");

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
                await p.Writer.WriteLineAsync($"\n\u001b[38;5;226m{player.Name} \u001b[38;5;118mentered the room.\u001b[0m");
            }

            if (room.Boss != null && room.Boss.HP > 0)
            {
                player.InCombat = true;
                await player.Writer.WriteLineAsync($"\u001b[38;5;118mA boss blocks your path: \u001b[38;5;118m{room.Boss.Name} \x1b[91m(HP: {room.Boss.HP})\x1b[0m");
                return;
            }

            var enemy = room.Npcs?.FirstOrDefault(n => n.IsEnemy && n.HP > 0);
            if (enemy != null)
            {
                player.InCombat = true;
                await player.Writer.WriteLineAsync($"\u001b[38;5;118mWatch out! An enemy is here: \x1b[38;5;208m{enemy.Name} \x1b[91m(HP: {enemy.HP})\x1b[0m");
            }

            await ShowRoom(player);
        }

        public async Task HandleTalk(Player player)
        {
            var room = player.CurrentRoom;

            if (room.Npcs == null || room.Npcs.Count == 0)
            {
                await player.Writer.WriteLineAsync("\u001b[38;5;118mThere is no one to talk to here.\u001b[0m");
                return;
            }

            player.TalkOptions = room.Npcs;

            await player.Writer.WriteLineAsync("\u001b[38;5;118mWho do you want to talk to?\u001b[0m");

            for (int i = 0; i < room.Npcs.Count; i++)
            {
                await player.Writer.WriteLineAsync($"{i + 1}. {room.Npcs[i].Name}");
            }

            await player.Writer.WriteLineAsync("\u001b[32mType number:\u001b[0m");
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
                await player.Writer.WriteLineAsync("\u001b[32mThere is nothing to attack here.\u001b[0m");
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
                    player.Writer.WriteLineAsync($"\x1b[38;5;208m{target.Name} \u001b[38;5;118mis already dead!\u001b[0m").Wait();
                    return;
                }

                target.HP -= playerDamage;
                int remainingHP = Math.Max(0, target.HP);

                GameLogger.Log($"[COMBAT]: {player.Name} damaged {target.Name} by {playerDamage} HP. Remains {remainingHP} HP.");

                Broadcast($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mhits \x1b[38;5;208m{target.Name} \u001b[38;5;118mfor \x1b[91m{playerDamage} \u001b[38;5;118mdamage! \x1b[91m(Enemy HP: {remainingHP})\x1b[0m", room).Wait();

                if (target.HP <= 0)
                {
                    enemyDefeated = true;
                }
            }

            if (enemyDefeated)
            {
                GameLogger.Log($"[COMBAT]: {target.Name} has been defeated in {room.Name}!");
                await Broadcast($"\n>>> \x1b[38;5;208m{target.Name} \u001b[38;5;118mwas defeated! \u001b[0m<<<", room);

                foreach (var p in _activePlayers.Where(p => p.CurrentRoom == room))
                {
                    p.InCombat = false;
                }

                if (target.Name.Contains("MiniBoss1")) player.Quests["Q1"] = true;
                if (target.Name.Contains("MiniBoss3")) player.HasKey = true;
                if (target.Name.Contains("FinalBoss")) await Broadcast("\n\u001b[38;5;118mYOU HAVE DEFEATED THE KING ORG! YOU WON THE GAME!\u001b[0m");

                await GiveLoot(player);
                return;
            }

            player.CurrentHealth -= enemyDamage;
            await player.Writer.WriteLineAsync($"\x1b[38;5;208m{target.Name} \u001b[38;5;118mhits YOU for \x1b[91m{enemyDamage} \u001b[38;5;118mdamage. \x1b[91m(Your HP: {player.CurrentHealth})\x1b[0m");

            if (player.CurrentHealth <= 0)
            {
                await Broadcast($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mwas killed by \x1b[38;5;208m{target.Name}!\x1b[0m", room);
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
                await player.Writer.WriteLineAsync("\u001b[38;5;118mYou found a Healing Potion!\u001b[0m");
                GameLogger.Log($"[LOOT]: {player.Name} found a Healing Potion.");
            }

            if (rnd.Next(0, 100) < 30)
            {
                player.Inventory.Add("Iron Weapon");
                player.AttackStrength = 8;
                await player.Writer.WriteLineAsync("\u001b[38;5;118mYou found an Iron Weapon!\u001b[0m");
                GameLogger.Log($"[LOOT]: {player.Name} found an Iron Weapon.");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mfound a weapon in the loot!\u001b[0m");
                }
            }

            if (rnd.Next(0, 100) < 20)
            {
                player.HasKey = true;
                await player.Writer.WriteLineAsync("\u001b[38;5;118mYou found THE KEY!\u001b[0m");
                GameLogger.Log($"[LOOT]: {player.Name} FOUND THE KEY!");
                await Broadcast($"\n>>> \u001b[38;5;226m{player.Name} \u001b[38;5;118mFOUND THE VAULT KEY! \u001b[0m<<<", room);
            }
        }
        public async Task ShowInventory(Player player)
        {
            await player.Writer.WriteLineAsync("=== INVENTORY ===");

            if (player.Inventory.Count == 0)
            {
                await player.Writer.WriteLineAsync("\u001b[32mEmpty\u001b[0m");
                return;
            }

            for (int i = 0; i < player.Inventory.Count; i++)
            {
                await player.Writer.WriteLineAsync($"\u001b[32m{i + 1}. {player.Inventory[i]}\u001b[0m");
            }

            await player.Writer.WriteLineAsync("\u001b[32mUse: equip <number>\u001b[0m");
        }

        public async Task HandleEquip(Player player, string arg)
        {
            if (!int.TryParse(arg, out int index))
            {
                await player.Writer.WriteLineAsync("\u001b[32mInvalid item number.\u001b[0m");
                return;
            }

            index--;

            if (index < 0 || index >= player.Inventory.Count)
            {
                await player.Writer.WriteLineAsync("\u001b[32mOut of range.\u001b[0m");
                return;
            }

            string item = player.Inventory[index];
            var room = player.CurrentRoom;


            if (item == "Healing Potion")
            {
                player.CurrentHealth = player.MaxHealth;
                player.Inventory.RemoveAt(index);

                GameLogger.Log($"[ITEM]: {player.Name} used a Healing Potion.");

                await player.Writer.WriteLineAsync("\u001b[38;5;118mYou drank a Healing Potion. Your HP is fully restored!\u001b[0m");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mdrank a Healing Potion.\u001b[0m");
                }
                return;
            }

            if (item.Contains("Weapon"))
            {
                player.EquippedWeapon = item;
                player.AttackStrength = 8;
                GameLogger.Log($"[ITEM]: {player.Name} has equipped {item}.");
                await player.Writer.WriteLineAsync($"\u001b[32mEquipped weapon: \u001b[0m{item}\u001b[0m");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mequipped an \u001b[0m{item}\u001b[0m.");
                }
            }
            else if (item.Contains("Armor"))
            {
                GameLogger.Log($"[ITEM]: {player.Name} has equipped {item}.");
                await player.Writer.WriteLineAsync($"\u001b[32mEquipped armor: \u001b[0m{item}\u001b[0m");

                var otherPlayers = _activePlayers.Where(p => p.CurrentRoom == room && p != player);
                foreach (var p in otherPlayers)
                {
                    await p.Writer.WriteLineAsync($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mput on \u001b[m{item}.\u001b[0m");
                }
            }
            else
            {
                await player.Writer.WriteLineAsync($"\u001b[32mUsed item: {item}\u001b[0m");
            }

            if (item == "Iron Pickaxe")
            {
                var shaft1 = _map.GetRoom("Shaft1");
                if (shaft1 != null && shaft1.Blocked)
                {
                    shaft1.Blocked = false;
                    GameLogger.Log($"[WORLD]: {player.Name} used an Iron Pickaxe and opened Shaft1.");
                    await player.Writer.WriteLineAsync("\u001b[38;5;118mYou used the Iron Pickaxe to clear the rubble!\u001b[0m");
                    await Broadcast($"\n>>> \u001b[38;5;226m{player.Name} \u001b[38;5;118mcleared the path to Shaft1! \u001b[0m<<<");
                }
                else
                {
                    await player.Writer.WriteLineAsync("\u001b[38;5;118mThere is nothing to mine here.\u001b[0m");
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
                        GameLogger.Log($"[QUEST]: {player.Name} completed a quest from the Miner (Q1).");
                        await player.Writer.WriteLineAsync("\u001b[38;5;118mQuest 1 completed! You got an Iron Pickaxe.\u001b[0m");
                        await Broadcast($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mcompleted a quest for \x1b[38;5;208m{npc.Name} \u001b[38;5;118mand got a reward!\x1b[0m", room);
                        return;
                    }
                }

                if (npc.Name.Contains("RiddleMaster") && !player.Quests.ContainsKey("Q3"))
                {
                    if (input.ToLower().Contains("growth") || input.ToLower().Contains("age"))
                    {
                        player.Quests["Q3"] = true;
                        GameLogger.Log($"[QUEST]: {player.Name} guessed the riddle from RiddleMaster (Q3).");
                        await player.Writer.WriteLineAsync("\u001b[38;5;118mCorrect! Riddle solved.\u001b[0m");
                        await Broadcast($"\u001b[38;5;226m{player.Name} \u001b[38;5;118mcorrectly answered the RiddleMaster's riddle!\u001b[0m", room);
                        return;
                    }
                }
            }
        }

        public async Task ShowStats(Player player)
        {
            await player.Writer.WriteLineAsync("\n=== PLAYER STATS ===");
            await player.Writer.WriteLineAsync($"Name:     \u001b[38;5;226m{player.Name}\u001b[0m");
            await player.Writer.WriteLineAsync($"HP:       \x1b[91m{player.CurrentHealth} / {player.MaxHealth}\x1b[0m");
            await player.Writer.WriteLineAsync($"Attack:   \x1b[91m{player.AttackStrength}\x1b[0m");
            await player.Writer.WriteLineAsync($"Weapon:   {player.EquippedWeapon}");
            await player.Writer.WriteLineAsync($"Deaths:   \x1b[91m{player.Deaths}\x1b[0m");
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
