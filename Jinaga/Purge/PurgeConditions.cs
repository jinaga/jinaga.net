using System;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Jinaga;
using Jinaga.Extensions;
using Jinaga.Projections;

/// <summary>
/// A set of conditions that must be met in order to purge a fact.
/// </summary>
public class PurgeConditions
{
    public static PurgeConditions Empty = new PurgeConditions(ImmutableList<Specification>.Empty);

    private ImmutableList<Specification> specifications;

    internal PurgeConditions(ImmutableList<Specification> specifications)
    {
        this.specifications = specifications;
    }

    /// <summary>
    /// Purge a fact when a successor fact exists that matches the specification.
    /// </summary>
    /// <typeparam name="TFact">The type of fact to purge</typeparam>
    /// <returns></returns>
    public PurgeClause<TFact> Purge<TFact>() where TFact : class
    {
        return new PurgeClause<TFact>(specifications);
    }

    /// <summary>
    /// Purge a fact when a successor fact exists that matches the specification.
    /// </summary>
    /// <param name="specification">The specification that the successor must match</param>
    /// <returns></returns>
    public PurgeConditions WhenExists(Specification specification)
    {
        return new PurgeConditions(specifications.Add(specification));
    }

    /// <summary>
    /// Compose sets of purge conditions.
    /// </summary>
    /// <param name="builder">A function that defines purge conditions</param>
    /// <returns></returns>
    public PurgeConditions With(Func<PurgeConditions, PurgeConditions> builder)
    {
        return builder(this);
    }

    /// <summary>
    /// Validate that the purge conditions are well-formed.
    /// </summary>
    /// <returns>The list of specifications</returns>
    /// <exception cref="InvalidOperationException">If there is an issue with the purge conditions</exception>
    public ImmutableList<Specification> Validate()
    {
        var validationFailures = specifications
            .SelectMany(s => PurgeFunctions.ValidatePurgeSpecification(s));
        if (validationFailures.Any())
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationFailures));
        }
        return specifications;
    }
}

public class PurgeClause<TFact> where TFact : class
{
    private readonly ImmutableList<Specification> specifications;

    internal PurgeClause(ImmutableList<Specification> specifications)
    {
        this.specifications = specifications;
    }

    /// <summary>
    /// Specify the condition under which to purge a fact.
    /// </summary>
    /// <typeparam name="TProjection">The type of fact that triggers the purge</typeparam>
    /// <param name="predecessorSelector">The relationship of the successor to the purged fact</param>
    /// <returns></returns>
    public PurgeConditions WhenExists<TProjection>(Expression<Func<TProjection, TFact>> predecessorSelector)
        where TProjection : class
    {
        var specification = Given<TFact>.Match(fact => fact.Successors().OfType(predecessorSelector));
        return new PurgeConditions(specifications.Add(specification));
    }
}