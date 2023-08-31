using System;

namespace Jinaga
{
    public class AuthorizationRules
    {
        public AuthorizationRules Any<T>()
        {
            throw new NotImplementedException();
        }

        public AuthorizationRules Type<T>(Func<T, object> predecessorSelector)
        {
            throw new NotImplementedException();
        }

        public static string Describe(Func<AuthorizationRules, AuthorizationRules> authorization)
        {
            throw new NotImplementedException();
        }
    }
}