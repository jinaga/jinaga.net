using Jinaga.Repository;
using System;
using System.Collections.Immutable;

namespace Jinaga
{
    public class AuthorizationRules
    {
        private ImmutableDictionary<string, ImmutableList<AuthorizationRule>> rulesByType;

        private AuthorizationRules(ImmutableDictionary<string, ImmutableList<AuthorizationRule>> rulesByType)
        {
            this.rulesByType = rulesByType;
        }

        public AuthorizationRules Any<T>()
        {
            return WithRule(typeof(T).FactTypeName(), new AuthorizationRuleAny());
        }

        private AuthorizationRules WithRule(string type, AuthorizationRule authorizationRule)
        {
            if (!rulesByType.TryGetValue(type, out var rules))
            {
                rules = ImmutableList<AuthorizationRule>.Empty;
            }
            rules = rules.Add(authorizationRule);
            rulesByType.SetItem(type, rules);
            return new AuthorizationRules(rulesByType);
        }

        public AuthorizationRules Type<T>(Func<T, object> predecessorSelector)
        {
            throw new NotImplementedException();
        }

        public static string Describe(Func<AuthorizationRules, AuthorizationRules> authorization)
        {
            var rules = authorization(new AuthorizationRules(ImmutableDictionary<string, ImmutableList<AuthorizationRule>>.Empty));
            string description = rules.SaveToDescription();
            return description;
        }

        private string SaveToDescription()
        {
            throw new NotImplementedException();
        }
    }
}