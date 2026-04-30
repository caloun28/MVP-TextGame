using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class UserManager
    {
        private Dictionary<string, string> _users;
        private readonly string _filePath = "users.json";

        public UserManager()
        {
            _users = new Dictionary<string, string>();
            LoadUsers();
        }

        private void LoadUsers()
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);

                _users = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                GameLogger.Log($"[UserManager]: Loaded {_users.Count} users from disk.");
            }
            else
            {
                GameLogger.Log("[UserManager]: File users.json not found. Starting with an empty database.");
            }
        }

        private void SaveUsers()
        {
            try
            {
                string json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
                GameLogger.Log($"[DATABASE]: User data has been successfully saved. Total players: {_users.Count}");
            }
            catch (Exception ex)
            {
                GameLogger.Log($"[DATABASE ERROR]: Failed to save the user: {ex.Message}");
            }
        }

        public bool Register(string username, string password)
        {
            if (_users.ContainsKey(username))
            {
                GameLogger.Log($"[UserManager]: Attempt to register an existing username '{username}'.");
                return false;
            }

            string hashedPasswdord = HashPaswd.HashPassword(password);
            _users.Add(username, hashedPasswdord);

            SaveUsers();

            GameLogger.Log($"[UserManager]: New user '{username}' has been registered succesfully.");
            return true;
        }

        public bool Login(string username, string password)
        {
            if (!_users.ContainsKey(username))
            {
                GameLogger.Log($"[UserManager]: Attempt to log in to a non-existent account '{username}'.");
                return false;
            }

            string hashedPassword = _users[username];
            bool success = HashPaswd.Verification(password, hashedPassword);

            if (success)
            {
                GameLogger.Log($"[UserManager]: User '{username}' has succesfully loged in.");
            }
            else
            {
                GameLogger.Log($"[UserManager]: Incorrect password for user '{username}'.");
            }

            return success;
        }

        public Dictionary<string, string> GetAllUsers()
        {
            return _users;
        }
    }
}
