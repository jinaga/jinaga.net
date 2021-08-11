using System;
using System.Collections.Generic;

namespace Jinaga.Generators
{
    class CodeExpressionVisitor
    {
        private Func<string?> recommender;
        private Func<IEnumerable<CodeExpressionVisitor>> generator;

        public CodeExpressionVisitor(Func<string?> recommender, Func<IEnumerable<CodeExpressionVisitor>> generator)
        {
            this.recommender = recommender;
            this.generator = generator;
        }

        public string? Recommend() => recommender();
        public IEnumerable<CodeExpressionVisitor> Generate() => generator();
    }
}
