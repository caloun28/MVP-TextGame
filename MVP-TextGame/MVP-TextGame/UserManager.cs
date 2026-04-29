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
                GameLogger.Log($"[UserManager]: Načteno {_users.Count} uživatelů z disku.");
            }
            else
            {
                GameLogger.Log("[UserManager]: Soubor users.json nenalezen, začínáme s prázdnou databází.");
            }
        }

        private void SaveUsers()
        {
            try
            {
                string json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
                GameLogger.Log($"[DATABASE]: Data uživatelů byla úspěšně zapsána. Celkem hráčů: {_users.Count}");
            }
            catch (Exception ex)
            {
                GameLogger.Log($"[DATABASE ERROR]: Nepodařilo se uložit uživatele: {ex.Message}");
            }
        }

        public bool Register(string username, string password)
        {
            if (_users.ContainsKey(username))
            {
                GameLogger.Log($"[UserManager]: Pokus o registraci existujícího jména '{username}'.");
                return false;
            }

            string hashedPasswdord = HashPaswd.HashPassword(password);
            _users.Add(username, hashedPasswdord);

            SaveUsers();

            GameLogger.Log($"[UserManager]: Nový uživatel '{username}' byl úspěšně zaregistrován.");
            return true;
        }

        public bool Login(string username, string password)
        {
            if (!_users.ContainsKey(username))
            {
                GameLogger.Log($"[UserManager]: Pokus o přihlášení na neexistující účet '{username}'.");
                return false;
            }

            string hashedPassword = _users[username];
            bool success = HashPaswd.Verification(password, hashedPassword);

            if (success)
            {
                GameLogger.Log($"[UserManager]: Uživatel '{username}' se úspěšně přihlásil.");
            }
            else
            {
                GameLogger.Log($"[UserManager]: Špatné heslo pro uživatele '{username}'.");
            }

            return success;
        }

        public Dictionary<string, string> GetAllUsers()
        {
            return _users;
        }
    }
}
