using Jinaga.Facts;
using Jinaga.Projections;
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
            using var hashAlgorithm = SHA512.Create();
            var hashBytes = hashAlgorithm.ComputeHash(bytes);
            var hashString = Convert.ToBase64String(hashBytes);
            return hashString;
        }

        public static string ComputeSpecificationHash(Specification specification, FactReferenceTuple givenTuple)
        {
            string declarationString = specification.GenerateDeclarationString(givenTuple);
            string specificationString = specification.ToDescriptiveString();
            string request = $"{declarationString}\n{specificationString}";
            string hashString = ComputeStringHash(request);
            return hashString;
        }
    }
}
