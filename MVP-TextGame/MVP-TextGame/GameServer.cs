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
        private GameEngine _engine;
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
                throw new Exception("Map failed to load [Rooms is null]");

            _engine = new GameEngine(_map, _activePlayers);
        }

        public async Task StartAsync()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _port);

            try
            {
                listener.Start();
                GameLogger.Log($"[SERVER]: Running on port {_port}. Waiting for players...");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    GameLogger.Log($"[NETWORK]: A new user has joined from an endpointu {client.Client.RemoteEndPoint}");

                    if (_activePlayers.Count >= _maxPlayers)
                    {
                        using (StreamWriter writer = new StreamWriter(client.GetStream(), Encoding.UTF8) { AutoFlush = true })
                        {
                            await writer.WriteLineAsync("\x1b[32mBoth players are joined, game is already running\x1b[0m");
                        }
                    }

                    Task.Run(() => HandleClientAsync(client));
                }
            }
            catch (Exception e)
            {
                GameLogger.Log($"[SERVER ERROR]: Critical error on server side: {e.Message}");
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
                    throw new Exception("\x1b[91mSpawn room not found! Check map.json loading.\x1b[0m");
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
                GameLogger.Log($"[NETWORK]: Client ({endpoint}) has unexpectedly disconnected: {ex.Message}");
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

                await writer.WriteLineAsync("\x1b[92mWelcome to the ABANDONED MINE!\x1b[0m");
                await writer.WriteLineAsync("\x1b[32mEnter [1] to login or [2] to register\x1b[0m");

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
                            bool isAlreadyPlaying;
                            lock (_activePlayers)
                            {
                                isAlreadyPlaying = _activePlayers.Any(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
                            }

                            if (isAlreadyPlaying)
                            {
                                await writer.WriteLineAsync("\x1b[32mThis account is already logged in from another location!\x1b[0m");
                                await writer.WriteLineAsync("\x1b[32mPlease try again or use a different account.\n\x1b[0m");
                                continue;
                            }

                            await writer.WriteLineAsync("\x1b[32mLogin successful\x1b[0m");
                            return playerName;
                        }
                        else
                        {
                            await writer.WriteLineAsync("\x1b[32mInvalid username or password. Try again.\x1b[0m");
                        }
                        break;

                    case "2":
                        await writer.WriteLineAsync("Create new username:");
                        playerName = await reader.ReadLineAsync();
                        await writer.WriteLineAsync("Create new password:");
                        string newPassword = await reader.ReadLineAsync();

                        if (_userManager.Register(playerName, newPassword))
                        {
                            await writer.WriteLineAsync("\x1b[92mRegistration successful. You are now logged in.\x1b[0m");
                            return playerName;
                        }
                        else
                        {
                            await writer.WriteLineAsync("\x1b[32mUsername already exists. Try again.\x1b[0m");
                        }
                        break;

                    default:
                        await writer.WriteLineAsync("\x1b[32mInvalid choice. Please enter [1] to login or [2] to register.\x1b[0m");
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

                if (string.IsNullOrEmpty(input) || input.ToLower() == "exit")
                {
                    await player.Writer.WriteLineAsync("\x1b[32mExiting the game. Goodbye!\x1b[0m");
                    isPlaying = false;
                    break;
                }
                await _engine.ProcessCommand(player, input);
            }
        }

        public async Task WaitForPlayer(Player player)
        {
            if (_activePlayers.Count < _maxPlayers)
            {
                await player.Writer.WriteLineAsync($"\x1b[32mYou are connected as {player.Name}.\x1b[0m");
                await player.Writer.WriteLineAsync("\x1b[32mWaiting for another player to join...\x1b[0m");

                while (_activePlayers.Count < _maxPlayers)
                {
                    await Task.Delay(500);
                }
            }

            await player.Writer.WriteLineAsync("\x1b[32mBoth players are connected. Starting the game...\x1b[0m");
            await player.Writer.WriteLineAsync("\x1b[92mFor help type 'help'\x1b[0m");

            await _engine.ShowRoom(player);
        }

        public void DisconnectPlayer(Player player)
        {
            lock (_activePlayers)
            {
                if (_activePlayers.Contains(player))
                {
                    _activePlayers.Remove(player);
                    GameLogger.Log($"[NETWORK]: Player {player.Name} has left the game.");
                }
            }
            player.Client.Close();
        }
    }
}
