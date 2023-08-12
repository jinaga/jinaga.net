using System;
using System.Security.Cryptography;
using System.Text;

namespace Jinaga.Identity
{
    internal class IdentityUtilities
    {
        public static string ComputeStringHash(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            using var hashAlgorithm = HashAlgorithm.Create("SHA-512");
            var hashBytes = hashAlgorithm.ComputeHash(bytes);
            var hashString = Convert.ToBase64String(hashBytes);
            return hashString;
        }
    }
}
