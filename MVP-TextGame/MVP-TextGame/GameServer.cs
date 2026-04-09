using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class GameServer
    {
        private readonly int _port;

        private UserManager _userManager = new UserManager();
        private List<Player> _activePlayers;
        private int _maxPlayers = 2;

        public GameServer(int port)
        {
            _port = port;
            _activePlayers = new List<Player>();
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
                string command = await player.Reader.ReadLineAsync();

                if (string.IsNullOrEmpty(command))
                {
                    break;
                }

                Console.WriteLine($"Received command from {player.Name}: {command}");

                switch (command.ToLower())
                {
                    case "exit":
                        await player.Writer.WriteLineAsync("Exiting the game. Goodbye!");
                        isPlaying = false;
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

            if(player.Deaths == 3)
            {
                await player.Writer.WriteLineAsync("[HELP]: Try to find better weapons and do sidequests.");
            }
        }
    }
}
