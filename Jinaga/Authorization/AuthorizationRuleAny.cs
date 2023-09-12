namespace Jinaga
{
    public class AuthorizationRuleAny : AuthorizationRule
    {
        public AuthorizationRuleAny()
        {
        }

        public override string Describe(string type)
        {
            return $"    any {type}\n";
        }
    }
}