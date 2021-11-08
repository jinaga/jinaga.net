using System;

namespace Jinaga.Generators
{
    public class CodeExpression
    {
        public CodeExpression(Type type, string code)
        {
            Type = type;
            Code = code;
        }

        public Type Type { get; }
        public string Code { get; }
    }
}