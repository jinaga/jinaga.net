using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    public class Specification
    {
        public Specification(
            ImmutableList<SpecificationGiven> givens,
            ImmutableList<Match> matches,
            Projection projection)
        {
            Givens = givens;
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<SpecificationGiven> Givens { get; }
        public ImmutableList<Match> Matches { get; }
        public Projection Projection { get; }

        /// <summary>
        /// Whether a fact graph can answer this specification, which is whether it has a
        /// graph form. That form is the only definition of the question: there is no
        /// second predicate to disagree with it, and nothing it admits can reach the
        /// evaluator as a shape the evaluator would skip.
        /// </summary>
        public bool CanRunOnGraph => GraphSpecification.From(this, out _) != null;

        public Specification Apply(ImmutableList<string> arguments)
        {
            var replacements = Givens.Zip(arguments, (parameter, argument) => (parameter.Label, argument))
                .ToImmutableDictionary(pair => pair.Label.Name, pair => pair.argument);
            var newMatches = Matches
                .Select(match =>
                    match.Apply(replacements)
                )
                .ToImmutableList();
            var newProjection = Projection.Apply(replacements);
            return new Specification(ImmutableList<SpecificationGiven>.Empty, newMatches, newProjection);
        }

        public Specification WithProjection(Projection projection)
        {
            return new Specification(Givens, Matches, projection);
        }

        /// <summary>
        /// Evaluates the specification over a fact graph.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The specification has no graph form. Read it from the store instead.
        /// </exception>
        public ImmutableList<Product> Execute(FactReferenceTuple givenTuple, FactGraph graph)
        {
            var graphSpecification = GraphSpecification.From(this, out var reason);
            if (graphSpecification == null)
            {
                throw new ArgumentException(
                    $"A fact graph cannot answer this specification, because {reason}. " +
                    "Read it from the store instead.");
            }
            return graphSpecification.Execute(givenTuple, graph);
        }

        public ImmutableList<Inverse> ComputeInverses()
        {
            return Inverter.InvertSpecification(this);
        }

        public string ToDescriptiveString(int depth = 0)
        {
            var indent = new string(' ', depth * 4);
            var given = string.Join(", ", this.Givens.Select(g => $"{g.Label.Name}: {g.Label.Type}{Conditions(g.ExistentialConditions, depth)}"));
            var matches = string.Join("", this.Matches.Select(m => m.ToDescriptiveString(depth + 1)));
            var projection = this.Projection is CompoundProjection cp && !cp.Names.Any() ? "" : " => " + this.Projection.ToDescriptiveString(depth);
            return $"{indent}({given}) {{\n{matches}{indent}}}{projection}\n";
        }

        private string Conditions(ImmutableList<ExistentialCondition> existentialConditions, int depth)
        {
            if (existentialConditions.Count == 0)
            {
                return string.Empty;
            }
            var indent = new string(' ', depth * 4);
            var conditions = string.Join("", existentialConditions.Select(c => c.ToDescriptiveString(this.Givens.First().Label.Name, depth + 1)));
            return $" [\n{conditions}{indent}]";
        }

        internal string GenerateDeclarationString(FactReferenceTuple given)
        {
            var startStrings = Givens.Select(g =>
            {
                var reference = given.Get(g.Label.Name);
                return $"let {g.Label.Name}: {g.Label.Type} = #{reference.Hash}\n";
            });
            return string.Join("", startStrings);
        }

        public override string ToString()
        {
            return ToDescriptiveString();
        }

        /// <summary>
        /// The same givens and matches, with every projection that is not a collection
        /// stripped out. What a feed registration means is "these givens, these matches,
        /// these collections", so two specifications that differ only in how they shape
        /// the local result reduce to the same specification and declare the same feeds.
        /// </summary>
        /// <remarks>
        /// Reducing a specification that is already reduced returns an equivalent one, so
        /// a caller that cannot tell whether its input has been reduced may reduce it
        /// again.
        /// </remarks>
        internal Specification Reduce()
        {
            return new Specification(
                Givens,
                Matches,
                ProjectionWithOnlyCollections(Projection));
        }

        private static Projection ProjectionWithOnlyCollections(Projection projection)
        {
            return MaybeProjectionWithOnlyCollections(projection) ??
                new CompoundProjection(ImmutableDictionary<string, Projection>.Empty, typeof(object));
        }

        private static Projection? MaybeProjectionWithOnlyCollections(Projection projection)
        {
            if (projection is CollectionProjection collectionProjection)
            {
                return new CollectionProjection(
                    collectionProjection.Matches,
                    ProjectionWithOnlyCollections(collectionProjection.Projection),
                    projection.Type
                );
            }
            else if (projection is CompoundProjection compoundProjection)
            {
                var namedProjections = compoundProjection.Names
                    .Select(name => (name, projection: MaybeProjectionWithOnlyCollections(compoundProjection.GetProjection(name))))
                    .Where(p => p.projection != null)
                    .ToImmutableDictionary(
                        p => p.name,
                        p => p.projection!
                    );
                return new CompoundProjection(namedProjections, projection.Type);
            }
            else
            {
                return null;
            }
        }
    }
}
