using MVP_TextGame.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class Player
    {
        public string Name { get; set; }
        public TcpClient Client { get; set; }
        public StreamReader Reader { get; set; }
        public StreamWriter Writer { get; set; }
        public Room CurrentRoom { get; set; }
        public List<Npc> TalkOptions { get; set; }

        private int maxHealth = 100;
        private int currentHealth;
        private int attackStrength = 10;
        private int deaths = 0;
        private List<string> inventory = new List<string>();
        private List<string> statusEffects = new List<string>();
        private string currentLocation = "";
        private bool isAlive = true;

        public int MaxHealth
        {
            get { return maxHealth; }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentException("Max health must be bigger than 0.");
                }
                maxHealth = value;
            }
        }

        public int CurrentHealth
        {
            get { return currentHealth; }
            set
            {
                if (value < 0)
                {
                    currentHealth = 0;
                }
                else if (value > MaxHealth)
                {
                    currentHealth = MaxHealth;
                }
                else
                {
                    currentHealth = value;
                }
            }
        }

        public int AttackStrength
        {
            get { return attackStrength; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Attack strength must be bigger than 0.");
                }
                attackStrength = value;
            }
        }

        public int Deaths
        {
            get { return deaths; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentException("Deaths must be bigger or 0.");
                }
                deaths = value;
            }
        }

        public List<string> StatusEffects
        {
            get
            {
                return statusEffects;
            }
        }

        public List<string> Inventory
        {
            get { return inventory; }
        }

        public string CurrentLocation
        {
            get { return currentLocation; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Current location cannot be empty.");
                }
                currentLocation = value;
            }
        }

        public bool IsAlive
        {
            get { return isAlive; }
            set { isAlive = value; }
        }

        public Player(string name, TcpClient client, StreamReader reader, StreamWriter writer)
        {
            Name = name;
            Client = client;
            Reader = reader;
            Writer = writer;

            TalkOptions = null;
        }
    }
}
