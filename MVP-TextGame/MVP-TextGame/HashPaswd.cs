using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MVP_TextGame
{
    public class HashPaswd
    {
        private int _move = 5;

        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";

            char[] chars = password.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                char oneChar = chars[i];

                oneChar = (char)(oneChar + _move);

                chars[i] = oneChar;
            }
            return new string(chars);
        }

        public bool Verification(string password, string hashedPassword)
        {
            string hashedInput = HashPassword(password);

            return hashedInput == hashedPassword;
        }
    }
}
