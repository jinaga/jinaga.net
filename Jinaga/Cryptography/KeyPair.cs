using System;
using System.Linq;
using System.Security.Cryptography;
using Jinaga.Facts;

namespace Jinaga.Cryptography
{
    public class KeyPair
    {
        public string PublicKey { get; }
        public string PrivateKey { get; }

        public KeyPair(string publicKey, string privateKey)
        {
            PublicKey = publicKey;
            PrivateKey = privateKey;
        }

        public static KeyPair Generate()
        {
            // Generate a new key pair.
            RSA rsa = RSA.Create();
            var publicKey = rsa.ExportRSAPublicKey();
            var privateKey = rsa.ExportRSAPrivateKey();

            // Write the public key in PEM format.
            var prefix = "-----BEGIN PUBLIC KEY-----\r\n";
            var suffix = "\r\n-----END PUBLIC KEY-----\r\n";
            var segments = Convert.ToBase64String(publicKey)
                .Select((c, i) => (c, i))
                .GroupBy(ci => ci.i / 64)
                .Select(g => new string(g.Select(ci => ci.c).ToArray()))
                .ToArray();
            var publicKeyPem = prefix + string.Join("\r\n", segments) + suffix;

            // Write the private key in PEM format.
            prefix = "-----BEGIN RSA PRIVATE KEY-----\r\n";
            suffix = "\r\n-----END RSA PRIVATE KEY-----\r\n";
            segments = Convert.ToBase64String(privateKey)
                .Select((c, i) => (c, i))
                .GroupBy(ci => ci.i / 64)
                .Select(g => new string(g.Select(ci => ci.c).ToArray()))
                .ToArray();
            var privateKeyPem = prefix + string.Join("\r\n", segments) + suffix;

            return new KeyPair(publicKeyPem, privateKeyPem);
        }

        public FactSignature SignFact(Fact fact)
        {
            // Convert the private key from PEM format.
            var privateKeyBase64 = PrivateKey
                .Replace("-----BEGIN RSA PRIVATE KEY-----", "")
                .Replace("-----END RSA PRIVATE KEY-----", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace(" ", "");
            var privateKeyBytes = Convert.FromBase64String(privateKeyBase64);

            // Canonicalize the fact.
            var canonicalString = Fact.Canonicalize(fact.Fields, fact.Predecessors);
            var bytes = System.Text.Encoding.UTF8.GetBytes(canonicalString);
            using var sha512 = SHA512.Create();
            var digest = sha512.ComputeHash(bytes);
            var hash = Convert.ToBase64String(digest);
            if (hash != fact.Reference.Hash)
            {
                throw new Exception("Hash does not match");
            }

            // Import the RSA key.
            using var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(privateKeyBytes, out var bytesRead);
            if (bytesRead != privateKeyBytes.Length)
            {
                throw new Exception("Failed to import private key");
            }

            // Sign the digest.
            var signature = rsa.SignHash(digest, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
            return new FactSignature(PublicKey, Convert.ToBase64String(signature));
        }
    }
}