using MVP_TextGame.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class GameServer
    {
        private readonly int _port;

        private GameMap _map;

        private UserManager _userManager = new UserManager();
        private List<Player> _activePlayers;
        private int _maxPlayers = 2;

        public GameServer(int port)
        {
            _port = port;
            _activePlayers = new List<Player>();

            string json = File.ReadAllText("map.json");

            _map = JsonSerializer.Deserialize<GameMap>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (_map?.Rooms == null)
                throw new Exception("Map failed to load (Rooms is null)");
        }

        public async Task StartAsync()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _port);

            try
            {
                listener.Start();
                Console.WriteLine($"Server started at {_port}. Waiting for players..");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

                    if (_activePlayers.Count >= _maxPlayers)
                    {
                        using (StreamWriter writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true })
                        {
                            await writer.WriteLineAsync("Both players are joined, game is already running");
                        }
                    }

                    Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error on server side: {e.Message}");
            }
        }

        public async Task HandleClientAsync(TcpClient client)
        {
            string endpoint = client.Client.RemoteEndPoint.ToString();
            Player player = null;
            try
            {
                StreamReader reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                StreamWriter writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true };

                string playerName = await VerifiPlayerAsync(reader, writer);
                if (playerName == null) return;

                player = new Player(playerName, client, reader, writer);
                var startRoom = _map?.GetRoom("Spawn");

                if (startRoom == null)
                {
                    throw new Exception("Spawn room not found! Check map.json loading.");
                }

                player.CurrentRoom = startRoom;
                lock (_activePlayers)
                {
                    _activePlayers.Add(player);
                }

                await WaitForPlayer(player);
                await GamePlay(player);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection to player ({endpoint}) has been disconnected: {ex.Message}");
            }
            finally
            {
                if (player != null)
                {
                    DisconnectPlayer(player);
                }
                else
                {
                    client.Close();
                }
            }
        }



        private async Task<string> VerifiPlayerAsync(StreamReader reader, StreamWriter writer)
        {
            while (true)
            {
                string playerName = "";

                await writer.WriteLineAsync("Welcome to the abandoned mine");
                await writer.WriteLineAsync("Enter [1] to login or [2] to register");

                string choice = await reader.ReadLineAsync();

                switch (choice)
                {
                    case "1":
                        await writer.WriteLineAsync("Enter username:");
                        playerName = await reader.ReadLineAsync();
                        await writer.WriteLineAsync("Enter password:");
                        string password = await reader.ReadLineAsync();

                        if (_userManager.Login(playerName, password))
                        {
                            await writer.WriteLineAsync("Login successful");
                            return playerName;
                        }
                        else
                        {
                            await writer.WriteLineAsync("Invalid username or password. Try again.");
                        }
                        break;

                    case "2":
                        await writer.WriteLineAsync("Create new username:");
                        playerName = await reader.ReadLineAsync();
                        await writer.WriteLineAsync("Create new password:");
                        string newPassword = await reader.ReadLineAsync();

                        if (_userManager.Register(playerName, newPassword))
                        {
                            await writer.WriteLineAsync("Registration successful. You can now login.");
                            Console.WriteLine($"New user registered: {playerName}");
                            return playerName;
                        }
                        else
                        {
                            await writer.WriteLineAsync("Username already exists. Try again.");
                        }
                        break;

                    default:
                        await writer.WriteLineAsync("Invalid choice. Please enter [1] to login or [2] to register.");
                        break;
                }
            }
        }


        public async Task GamePlay(Player player)
        {
            bool isPlaying = true;

            while (isPlaying)
            {
                string input = await player.Reader.ReadLineAsync();

                if (string.IsNullOrEmpty(input))
                {
                    break;
                }

                if (player.TalkOptions != null)
                {
                    if (int.TryParse(input, out int index))
                    {
                        index -= 1;

                        if (index >= 0 && index < player.TalkOptions.Count)
                        {
                            var npc = player.TalkOptions[index];
                            await player.Writer.WriteLineAsync($"{npc.Name} says: {npc.Dialogue}");
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
                    continue;
                }

                await HandleQuest(player, input);


                string[] parts = input.Split(' ', 2);
                string command = parts[0].ToLower();
                string argument = parts.Length > 1 ? parts[1] : "";

                if (string.IsNullOrEmpty(command))
                {
                    break;
                }

                Console.WriteLine($"Received command from {player.Name}: {command}");

                switch (command)
                {
                    case "exit":
                        await player.Writer.WriteLineAsync("Exiting the game. Goodbye!");
                        isPlaying = false;
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
        }

        public async Task WaitForPlayer(Player player)
        {
            if (_activePlayers.Count < _maxPlayers)
            {
                await player.Writer.WriteLineAsync($"You are connected as {player}.");
                await player.Writer.WriteLineAsync("Waiting for another player to join...");


                while (_activePlayers.Count < _maxPlayers)
                {
                    await Task.Delay(500);
                }
            }

            await player.Writer.WriteLineAsync("Both players are connected. Starting the game...");
            await player.Writer.WriteLineAsync("For help type 'help'");

            await ShowRoom(player);
        }

        public void DisconnectPlayer(Player player)
        {
            lock (_activePlayers)
            {
                if (_activePlayers.Contains(player))
                {
                    _activePlayers.Remove(player);
                    Console.WriteLine($"Player {player.Name} has disconnected.");
                }
            }
            player.Client.Close();
        }

        public async Task HelpOnPlayerDeath(Player player)
        {
            player.Deaths++;
            player.CurrentHealth = player.MaxHealth;

            await player.Writer.WriteLineAsync($"YOU HAVE DIED!!");
            await player.Writer.WriteLineAsync("You have been respawned at the start of the mine.");

            if (player.Deaths == 3)
            {
                await player.Writer.WriteLineAsync("[HELP]: Try to find better weapons and do sidequests.");
            }
        }

        private async Task HandleWhisper(Player sender, string message)
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

            await receiver.Writer.WriteLineAsync($"[Whisper from {sender.Name}]: {message}");
        }

        private async Task HandleGo(Player player, string roomName)
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

            if (nextRoom == null)
            {
                await player.Writer.WriteLineAsync("Room not found.");
                return;
            }

            if (nextRoom.Blocked)
            {
                await player.Writer.WriteLineAsync("The path is blocked. You need to clear it first.");
                return;
            }

            if (nextRoom.Type == "final" && !player.HasKey)
            {
                await player.Writer.WriteLineAsync("The door is locked. You need a key.");
                return;
            }

            if (nextRoom.Boss != null)
            {
                player.CurrentEnemy = nextRoom.Boss;
                player.CurrentEnemyHP = nextRoom.Boss.HP;
                player.InCombat = true;

                await player.Writer.WriteLineAsync($"A boss blocks your path: {nextRoom.Boss.Name}");
                return;
            }

            await EnterRoom(player, nextRoom);
        }

        private async Task ShowRoom(Player player)
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
        private async Task ShowAvailableRooms(Player player)
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
        private async Task EnterRoom(Player player, Room room)
        {
            player.CurrentRoom = room;

            await player.Writer.WriteLineAsync($"\n=== {room.Name} ===");
            await player.Writer.WriteLineAsync(room.Description);

            if (room.Type == "final" && !player.HasKey)
            {
                await player.Writer.WriteLineAsync("The door is locked. You need a KEY.");
                player.CurrentRoom = null;
                return;
            }

            if (room.Blocked)
            {
                await player.Writer.WriteLineAsync("This path is blocked.");
                return;
            }

            if (room.Boss != null && room.Boss.HP > 0)
            {
                player.CurrentEnemy = room.Boss;
                player.CurrentEnemyHP = room.Boss.HP;
                player.InCombat = true;

                await player.Writer.WriteLineAsync($"Boss blocks the path: {room.Boss.Name}");
                return;
            }

            await ShowRoom(player);
        }

        private async Task HandleTalk(Player player)
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

        private async Task HandleAttack(Player player)
        {
            if (player.CurrentEnemy == null)
            {
                await player.Writer.WriteLineAsync("There is nothing to attack.");
                return;
            }

            Random rnd = new Random();

            int playerDamage = player.AttackStrength + rnd.Next(-2, 5);
            int enemyDamage = player.CurrentEnemy.Attack + rnd.Next(-2, 3);

            player.CurrentEnemyHP -= playerDamage;

            await player.Writer.WriteLineAsync($"You deal {playerDamage} damage to {player.CurrentEnemy.Name}.");

            if (player.CurrentEnemyHP <= 0)
            {
                await player.Writer.WriteLineAsync($"{player.CurrentEnemy.Name} was defeated!");

                player.CurrentEnemy = null;
                player.InCombat = false;

                await GiveLoot(player);
                return;
            }

            player.CurrentHealth -= enemyDamage;

            await player.Writer.WriteLineAsync($"{player.CurrentEnemy.Name} hits you for {enemyDamage}.");

            if (player.CurrentHealth <= 0)
            {
                await HelpOnPlayerDeath(player);
            }

            if (player.CurrentEnemy != null && player.CurrentEnemy.HP <= 0)
            {
                if (player.CurrentEnemy.Name.Contains("MiniBoss 1"))
                    player.Quests["Q1"] = true;

                if (player.CurrentEnemy.Name.Contains("MiniBoss 3"))
                    player.HasKey = true;

                player.CurrentEnemy = null;
                player.InCombat = false;
            }
        }
        private async Task GiveLoot(Player player)
        {
            Random rnd = new Random();

            if (rnd.Next(0, 100) < 50)
            {
                player.Inventory.Add("Healing Potion");
            }

            if (rnd.Next(0, 100) < 30)
            {
                player.Inventory.Add("Iron Weapon");
                player.AttackStrength = 8;
            }

            if (rnd.Next(0, 100) < 20)
            {
                player.HasKey = true;
                await player.Writer.WriteLineAsync("You found THE KEY!");
            }
        }
        private async Task ShowInventory(Player player)
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

        private async Task HandleEquip(Player player, string arg)
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

            if (item.Contains("Weapon"))
            {
                player.EquippedWeapon = item;
                player.AttackStrength = 8;
                await player.Writer.WriteLineAsync($"Equipped weapon: {item}");
            }
            else if (item.Contains("Armor"))
            {
                await player.Writer.WriteLineAsync($"Equipped armor: {item}");
            }
            else
            {
                await player.Writer.WriteLineAsync($"Used item: {item}");
            }

            if (item == "Iron Pickaxe")
            {
                var shaft1 = _map.GetRoom("Shaft1");
                if (shaft1 != null)
                {
                    shaft1.Blocked = false;
                }
            }
        }
        private async Task HandleQuest(Player player, string input)
        {
            var room = player.CurrentRoom;

            if (room == null || room.Npcs == null)
                return;

            foreach (var npc in room.Npcs)
            {
                if (npc.Name.Contains("NPC 2") && !player.Quests["Q1"])
                {
                    if (input.ToLower().Contains("done"))
                    {
                        player.Quests["Q1"] = true;
                        player.Inventory.Add("Iron Pickaxe");
                        await player.Writer.WriteLineAsync("Quest 1 completed! You got Iron Pickaxe.");
                        return;
                    }
                }

                if (npc.Name.Contains("NPC 3") && !player.Quests["Q3"])
                {
                    if (input.ToLower().Contains("growth"))
                    {
                        player.Quests["Q3"] = true;
                        await player.Writer.WriteLineAsync("Correct! Quest 3 done.");
                        return;
                    }
                }

                if (npc.Name.Contains("NPC 4") && !player.Quests["Q4"])
                {
                    if (input.ToLower().Contains("done"))
                    {
                        player.Quests["Q4"] = true;
                        await player.Writer.WriteLineAsync("Puzzle solved! Quest 4 done.");
                        return;
                    }
                }
            }
        }
    }
}
