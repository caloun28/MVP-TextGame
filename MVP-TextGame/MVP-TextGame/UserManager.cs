using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class UserManager
    {

        private Dictionary<string, string> _users;

        public UserManager()
        {
            _users = new Dictionary<string, string>();
        }

        public bool Register(string username, string password)
        {
            if (_users.ContainsKey(username))
            {
                return false;
            }

            string hashedPasswdord = HashPaswd.HashPassword(password);

            _users.Add(username, hashedPasswdord);

            return true;
        }

        public bool Login(string username, string password)
        {
            if (!_users.ContainsKey(username))
            {
                return false;
            }
            string hashedPassword = _users[username];
            return HashPaswd.Verification(password, hashedPassword);
        }

        public Dictionary<string, string> GetAllUsers()
        {
            return _users;
        }
    }
}
