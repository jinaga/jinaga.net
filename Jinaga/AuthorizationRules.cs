using Jinaga.Projections;
using Jinaga.Repository;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;

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
            where T : class
        {
            return WithRule(typeof(T).FactTypeName(), new AuthorizationRuleAny());
        }

        public AuthorizationRules Type<T>(Expression<Func<T, User>> predecessorSelector)
            where T : class
        {
            Specification specification = Given<T>.Match(predecessorSelector);
            return WithRule(typeof(T).FactTypeName(), new AuthorizationRuleSpecification(specification));
        }

        public AuthorizationRules Type<T>(Expression<Func<T, FactRepository, IQueryable<User>>> specExpression)
            where T : class
        {
            Specification specification = Given<T>.Match(specExpression);
            return WithRule(typeof(T).FactTypeName(), new AuthorizationRuleSpecification(specification));
        }

        public static string Describe(Func<AuthorizationRules, AuthorizationRules> authorization)
        {
            var rules = authorization(new AuthorizationRules(ImmutableDictionary<string, ImmutableList<AuthorizationRule>>.Empty));
            string description = rules.SaveToDescription();
            return description;
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

        private string SaveToDescription()
        {
            throw new NotImplementedException();
        }
    }
}