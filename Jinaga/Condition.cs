using System;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public class Condition
    {
        public Expression<Func<FactRepositoryOld, bool>> Body { get; }

        public Condition(Expression<Func<FactRepositoryOld, bool>> body)
        {
            this.Body = body;
        }

        public static implicit operator bool(Condition c) => true;
    }
}
