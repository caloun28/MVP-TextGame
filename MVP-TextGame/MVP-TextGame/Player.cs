using MVP_TextGame.Models;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.IO;

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
        public Dictionary<string, bool> Quests { get; set; } = new Dictionary<string, bool>();

        public bool HasKey { get; set; } = false;

        public string PendingQuestInput { get; set; } = null;

        // ===== COMBAT =====
        public int MaxHealth { get; set; } = 100;
        public int CurrentHealth { get; set; } = 100;
        public int AttackStrength { get; set; } = 5;

        public bool InCombat { get; set; } = false;
        public Npc CurrentEnemy { get; set; }
        public int CurrentEnemyHP { get; set; }

        // ===== GAME STATS =====
        public int Deaths { get; set; } = 0;

        // ===== INVENTORY =====
        public List<string> Inventory { get; set; } = new List<string>();
        public string EquippedWeapon { get; set; } = "Bare Hands";

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