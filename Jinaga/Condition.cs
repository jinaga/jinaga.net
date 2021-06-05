using System;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public class Condition
    {
        private readonly Expression<Func<FactRepository, bool>> body;

        public Condition(Expression<Func<FactRepository, bool>> body)
        {
            this.body = body;
        }

        public static implicit operator bool(Condition c) => true;
    }
}
