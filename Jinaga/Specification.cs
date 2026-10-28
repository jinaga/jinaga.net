using Jinaga.Facts;
using Jinaga.Projections;
using Jinaga.Repository;
using Jinaga.Serialization;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Jinaga
{

    public static class Given<TFact>
        where TFact: class
    {
        /// <summary>
        /// Restricts the given to the facts that satisfy a condition. Returns a builder that
        /// offers the same <c>Match</c> and <c>Select</c> overloads, so the condition is
        /// written before the body of the specification:
        ///
        /// <code>
        /// Given&lt;Contact&gt;
        ///     .Where((contact, facts) =&gt; !facts.Any&lt;Deleted&gt;(d =&gt; d.contact == contact))
        ///     .Match((contact, facts) =&gt;
        ///         from name in facts.OfType&lt;Name&gt;()
        ///         where name.contact == contact
        ///         select name);
        /// </code>
        /// </summary>
        public static ConditionalGiven<TFact> Where(Expression<Func<TFact, FactRepository, bool>> condition)
        {
            return ConditionalGiven<TFact>.Unconditional.Where(condition);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            return ConditionalGiven<TFact>.Unconditional.Match(specExpression);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, IQueryable<TProjection>>> specExpression)
        {
            return ConditionalGiven<TFact>.Unconditional.Match(specExpression);
        }

        public static Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> specExpression)
        {
            return ConditionalGiven<TFact>.Unconditional.Match(specExpression);
        }

        public static Specification<TFact, TProjection> Select<TProjection>(Expression<Func<TFact, FactRepository, TProjection>> specSelector)
        {
            return ConditionalGiven<TFact>.Unconditional.Select(specSelector);
        }

        public static Specification<TFact, TProjection> Select<TProjection>(Expression<Func<TFact, TProjection>> specSelector)
        {
            return ConditionalGiven<TFact>.Unconditional.Select(specSelector);
        }
    }

    /// <summary>
    /// A given carrying the conditions written on it so far. Each condition is translated
    /// into the existential conditions of a <see cref="SpecificationGiven"/>, which the
    /// stores evaluate before they read the matches: a specification answers nothing while
    /// one of its givens does not satisfy its conditions.
    /// </summary>
    public class ConditionalGiven<TFact>
        where TFact : class
    {
        internal static readonly ConditionalGiven<TFact> Unconditional =
            new ConditionalGiven<TFact>(ImmutableList<LambdaExpression>.Empty);

        private readonly ImmutableList<LambdaExpression> conditions;

        private ConditionalGiven(ImmutableList<LambdaExpression> conditions)
        {
            this.conditions = conditions;
        }

        /// <summary>
        /// Adds a condition. Repeated calls are combined with and, exactly as the conjuncts
        /// of a single predicate are.
        /// </summary>
        public ConditionalGiven<TFact> Where(Expression<Func<TFact, FactRepository, bool>> condition)
        {
            return new ConditionalGiven<TFact>(GivenCondition.Add(conditions, condition));
        }

        public Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>(specExpression, conditions);
            return new Specification<TFact, TProjection>(givens, matches, projection);
        }

        public Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>(specExpression, conditions);
            return new Specification<TFact, TProjection>(givens, matches, projection);
        }

        public Specification<TFact, TProjection> Match<TProjection>(Expression<Func<TFact, TProjection>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>(specExpression, conditions);
            return new Specification<TFact, TProjection>(givens, matches, projection);
        }

        public Specification<TFact, TProjection> Select<TProjection>(Expression<Func<TFact, FactRepository, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector, conditions);
            return new Specification<TFact, TProjection>(givens, matches, projection);
        }

        public Specification<TFact, TProjection> Select<TProjection>(Expression<Func<TFact, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector, conditions);
            return new Specification<TFact, TProjection>(givens, matches, projection);
        }
    }

    public static class Given<TFact1, TFact2>
        where TFact1 : class
        where TFact2 : class
    {
        /// <summary>
        /// Restricts the givens to the facts that satisfy a condition. Each conjunct of the
        /// predicate becomes a condition on the last given, in declaration order, that it
        /// references, so a condition that joins both givens lands on the second and one
        /// that reads only the first lands on the first.
        /// </summary>
        public static ConditionalGiven<TFact1, TFact2> Where(Expression<Func<TFact1, TFact2, FactRepository, bool>> condition)
        {
            return ConditionalGiven<TFact1, TFact2>.Unconditional.Where(condition);
        }

        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            return ConditionalGiven<TFact1, TFact2>.Unconditional.Match(specExpression);
        }

        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, IQueryable<TProjection>>> specExpression)
        {
            return ConditionalGiven<TFact1, TFact2>.Unconditional.Match(specExpression);
        }

        public static Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specExpression)
        {
            return ConditionalGiven<TFact1, TFact2>.Unconditional.Match(specExpression);
        }

        public static Specification<TFact1, TFact2, TProjection> Select<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, TProjection>> specSelector)
        {
            return ConditionalGiven<TFact1, TFact2>.Unconditional.Select(specSelector);
        }

        public static Specification<TFact1, TFact2, TProjection> Select<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specSelector)
        {
            return ConditionalGiven<TFact1, TFact2>.Unconditional.Select(specSelector);
        }
    }

    /// <summary>
    /// Two givens carrying the conditions written on them so far.
    /// See <see cref="ConditionalGiven{TFact}"/>.
    /// </summary>
    public class ConditionalGiven<TFact1, TFact2>
        where TFact1 : class
        where TFact2 : class
    {
        internal static readonly ConditionalGiven<TFact1, TFact2> Unconditional =
            new ConditionalGiven<TFact1, TFact2>(ImmutableList<LambdaExpression>.Empty);

        private readonly ImmutableList<LambdaExpression> conditions;

        private ConditionalGiven(ImmutableList<LambdaExpression> conditions)
        {
            this.conditions = conditions;
        }

        /// <summary>
        /// Adds a condition. Repeated calls are combined with and, exactly as the conjuncts
        /// of a single predicate are.
        /// </summary>
        public ConditionalGiven<TFact1, TFact2> Where(Expression<Func<TFact1, TFact2, FactRepository, bool>> condition)
        {
            return new ConditionalGiven<TFact1, TFact2>(GivenCondition.Add(conditions, condition));
        }

        public Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>((LambdaExpression)specExpression, conditions);
            return new Specification<TFact1, TFact2, TProjection>(givens, matches, projection);
        }

        public Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, IQueryable<TProjection>>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Queryable<TProjection>((LambdaExpression)specExpression, conditions);
            return new Specification<TFact1, TFact2, TProjection>(givens, matches, projection);
        }

        public Specification<TFact1, TFact2, TProjection> Match<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specExpression)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Scalar<TProjection>((LambdaExpression)specExpression, conditions);
            return new Specification<TFact1, TFact2, TProjection>(givens, matches, projection);
        }

        public Specification<TFact1, TFact2, TProjection> Select<TProjection>(Expression<Func<TFact1, TFact2, FactRepository, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector, conditions);
            return new Specification<TFact1, TFact2, TProjection>(givens, matches, projection);
        }

        public Specification<TFact1, TFact2, TProjection> Select<TProjection>(Expression<Func<TFact1, TFact2, TProjection>> specSelector)
        {
            (var givens, var matches, var projection) = SpecificationProcessor.Select<TProjection>(specSelector, conditions);
            return new Specification<TFact1, TFact2, TProjection>(givens, matches, projection);
        }
    }

    public class Specification<TFact, TProjection> : Specification
        where TFact : class
    {
        public Specification(ImmutableList<SpecificationGiven> given, ImmutableList<Match> matches, Projections.Projection projection)
            : base(given, matches, projection)
        {
        }

        public string ToDescriptiveString(TFact given)
        {
            var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
            var givenReference = collector.Serialize(given);
            var givenTuple = FactReferenceTuple.Empty
                .Add(Givens.Single().Label.Name, givenReference);

            string startString = GenerateDeclarationString(givenTuple);
            string specificationString = ToDescriptiveString();
            return startString + "\n" + specificationString;
        }
    }

    public class Specification<TFact1, TFact2, TProjection> : Specification
        where TFact1: class
        where TFact2 : class
    {
        public Specification(ImmutableList<SpecificationGiven> given, ImmutableList<Match> matches, Projections.Projection projection)
            : base(given, matches, projection)
        {
        }
        public string ToDescriptiveString(TFact1 given1, TFact2 given2)
        {
            var collector = new Collector(SerializerCache.Empty, new ConditionalWeakTable<object, FactGraph>());
            var aReference = collector.Serialize(given1);
            var bReference = collector.Serialize(given2);
            var givenTuple = FactReferenceTuple.Empty
                .Add(Givens.ElementAt(0).Label.Name, aReference)
                .Add(Givens.ElementAt(1).Label.Name, bReference);

            string startString = GenerateDeclarationString(givenTuple);
            string specificationString = ToDescriptiveString();
            return startString + "\n" + specificationString;
        }
    }
}
