using System;
using System.Linq.Expressions;
using Jinaga.Repository;

namespace Jinaga
{
    public class Condition
    {
        public Expression Body { get; }

        public Condition(Expression<Func<FactRepository, bool>> body)
        {
            this.Body = body;
        }

        public Condition(Expression<Func<bool>> body)
        {
            this.Body = body;
        }

        public static implicit operator bool(Condition c) => true;

        public static Condition Define(Expression<Func<FactRepository, bool>> body)
        {
            return new Condition(body);
        }

        public static Condition Define(Expression<Func<bool>> body)
        {
            return new Condition(body);
        }
    }
}
