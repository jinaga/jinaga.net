namespace Jinaga
{
    public class AuthorizationRuleNone : AuthorizationRule
    {
        public AuthorizationRuleNone()
        {
        }

        public override string Describe(string type)
        {
            return $"    no {type}\n";
        }
    }
}