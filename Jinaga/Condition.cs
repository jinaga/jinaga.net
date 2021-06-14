using System;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public class Condition
    {
        public Expression<Func<FactRepository, bool>> Body { get; }

        public Condition(Expression<Func<FactRepository, bool>> body)
        {
            this.Body = body;
        }

        public static implicit operator bool(Condition c) => true;
    }
}
