using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

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
                var startRoom = _map?.GetRoom("Start");

                if (startRoom == null)
                {
                    throw new Exception("Start room not found! Check map.json loading.");
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

        private async Task HandleGo(Player player, string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
            {
                await player.Writer.WriteLineAsync("Usage: go <direction>");
                return;
            }

            var currentRoom = player.CurrentRoom;

            if (currentRoom.Connections == null || !currentRoom.Connections.ContainsKey(direction))
            {
                await player.Writer.WriteLineAsync("You can't go that way.");
                return;
            }

            string nextRoomName = currentRoom.Connections[direction];
            var nextRoom = _map.GetRoom(nextRoomName);

            if (nextRoom == null)
            {
                await player.Writer.WriteLineAsync("Room does not exist.");
                return;
            }

            player.CurrentRoom = nextRoom;

            await player.Writer.WriteLineAsync($"You moved to {nextRoom.Name}");

            await ShowRoom(player);
        }

        private async Task ShowRoom(Player player)
        {
            var room = player.CurrentRoom;

            await player.Writer.WriteLineAsync($"You are in: {room.Name}");
            await player.Writer.WriteLineAsync(room.Description);

            string exits = string.Join(", ", room.Connections.Keys);
            await player.Writer.WriteLineAsync($"Exits: {exits}");

            var playersHere = _activePlayers
                .Where(p => p != player && p.CurrentRoom == room)
                .Select(p => p.Name)
                .ToList();

            if (playersHere.Count > 0)
                await player.Writer.WriteLineAsync("Players here: " + string.Join(", ", playersHere));
            else
                await player.Writer.WriteLineAsync("Players here: none");

            if (room.Npcs != null && room.Npcs.Count > 0)
            {
                await player.Writer.WriteLineAsync("NPCs here: " +
                    string.Join(", ", room.Npcs.Select(n => n.Name)));
            }
            else
            {
                await player.Writer.WriteLineAsync("NPCs here: none");
            }
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
    }
}
