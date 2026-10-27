using Jinaga.Facts;
using Jinaga.Pipelines;
using Jinaga.Products;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jinaga.Projections
{
    /// <summary>
    /// A specification in the form the fact graph evaluates: givens that are bare labels,
    /// matches that walk predecessors only, and the collections the product carries.
    ///
    /// Nothing the evaluator cannot answer can be built. There is no member for an
    /// existential condition, a condition on a given, a successor step, a match with no
    /// path, a signer projection or a date-learned projection, so the evaluator never has
    /// to decide what to do with one and never silently skips it.
    ///
    /// <see cref="From"/> is the only way to make one, and so it is the one definition of
    /// what runs on the graph. Every caller that chooses between the graph and the store
    /// asks it.
    /// </summary>
    internal sealed class GraphSpecification
    {
        private GraphSpecification(
            ImmutableList<Label> givens,
            ImmutableList<GraphMatch> matches,
            GraphProjection projection)
        {
            Givens = givens;
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<Label> Givens { get; }
        public ImmutableList<GraphMatch> Matches { get; }
        public GraphProjection Projection { get; }

        /// <summary>
        /// The graph form of a specification, or null when it has none.
        /// </summary>
        /// <param name="reason">
        /// Names the shape that has no graph form, in the middle of a sentence, or null
        /// when the conversion succeeded.
        /// </param>
        public static GraphSpecification? From(Specification specification, out string? reason)
        {
            var converter = new Converter();
            var graphSpecification = converter.Convert(specification);
            reason = graphSpecification == null ? converter.Reason : null;
            return graphSpecification;
        }

        /// <summary>
        /// Whether the graph can run this match on its own, which is what
        /// <see cref="WellFormedSpecification.SplitBeforeFirstSuccessor"/> asks to find its
        /// pivot. It is the same conversion, so the head the split produces converts.
        /// </summary>
        public static bool CanRun(Match match)
        {
            return new Converter().ConvertMatch(match) != null;
        }

        public ImmutableList<Product> Execute(FactReferenceTuple givenTuple, FactGraph graph)
        {
            return ExecuteMatches(givenTuple, Matches, graph)
                .Select(tuple => CreateProduct(tuple, Projection, graph))
                .ToImmutableList();
        }

        private static ImmutableList<FactReferenceTuple> ExecuteMatches(
            FactReferenceTuple start, ImmutableList<GraphMatch> matches, FactGraph graph)
        {
            return matches.Aggregate(
                ImmutableList.Create(start),
                (set, match) => set
                    .SelectMany(references => ExecuteMatch(references, match, graph))
                    .ToImmutableList());
        }

        private static ImmutableList<FactReferenceTuple> ExecuteMatch(
            FactReferenceTuple references, GraphMatch match, FactGraph graph)
        {
            // Every path must hold, so the unknown is the intersection of their results.
            var result = match.AdditionalPaths.Aggregate(
                ExecutePath(references, match.Path, graph),
                (set, path) => set
                    .Intersect(ExecutePath(references, path, graph))
                    .ToImmutableList());
            return result
                .Select(reference => references.Add(match.Unknown.Name, reference))
                .ToImmutableList();
        }

        private static ImmutableList<FactReference> ExecutePath(
            FactReferenceTuple start, GraphPath path, FactGraph graph)
        {
            var set = ImmutableList.Create(start.Get(path.LabelRight));
            foreach (var role in path.Roles)
            {
                set = set
                    .SelectMany(reference => graph.Predecessors(reference, role.Name, role.TargetType))
                    .ToImmutableList();
            }
            return set;
        }

        private static Product CreateProduct(
            FactReferenceTuple tuple, GraphProjection projection, FactGraph graph)
        {
            // Every label of the tuple goes into the product, and that is all a simple,
            // field or hash projection reads back. Only a collection adds anything.
            var product = tuple.Names.Aggregate(
                Product.Empty,
                (p, name) => p.With(name, new SimpleElement(tuple.Get(name)))
            );
            foreach (var name in projection.Names)
            {
                var collection = projection.GetCollection(name);
                var products = ExecuteMatches(tuple, collection.Matches, graph)
                    .Select(inner => CreateProduct(inner, collection.Projection, graph))
                    .ToImmutableList();
                product = product.With(name, new CollectionElement(products));
            }
            return product;
        }

        /// <summary>
        /// Builds the graph form, or records why there is none. The first refusal it meets
        /// is the one it reports.
        /// </summary>
        private sealed class Converter
        {
            public string? Reason { get; private set; }

            private T? Refuse<T>(string reason) where T : class
            {
                if (Reason == null)
                {
                    Reason = reason;
                }
                return null;
            }

            public GraphSpecification? Convert(Specification specification)
            {
                var givens = ImmutableList.CreateBuilder<Label>();
                foreach (var given in specification.Givens)
                {
                    if (given.ExistentialConditions.Any())
                    {
                        return Refuse<GraphSpecification>(
                            $"the given '{given.Label.Name}' carries an existential condition");
                    }
                    givens.Add(given.Label);
                }
                var matches = ConvertMatches(specification.Matches);
                if (matches == null)
                {
                    return null;
                }
                var projection = ConvertProjection(specification.Projection);
                if (projection == null)
                {
                    return null;
                }
                return new GraphSpecification(givens.ToImmutable(), matches, projection);
            }

            public ImmutableList<GraphMatch>? ConvertMatches(ImmutableList<Match> matches)
            {
                var converted = ImmutableList.CreateBuilder<GraphMatch>();
                foreach (var match in matches)
                {
                    var graphMatch = ConvertMatch(match);
                    if (graphMatch == null)
                    {
                        return null;
                    }
                    converted.Add(graphMatch);
                }
                return converted.ToImmutable();
            }

            public GraphMatch? ConvertMatch(Match match)
            {
                var unknown = match.Unknown.Name;
                if (match.ExistentialConditions.Any())
                {
                    return Refuse<GraphMatch>($"the match of '{unknown}' carries an existential condition");
                }
                if (match.PathConditions.Count == 0)
                {
                    return Refuse<GraphMatch>($"the match of '{unknown}' has no path condition");
                }
                var paths = new List<GraphPath>();
                foreach (var pathCondition in match.PathConditions)
                {
                    if (pathCondition.RolesLeft.Count > 0)
                    {
                        return Refuse<GraphMatch>(
                            $"the match of '{unknown}' seeks successors of '{pathCondition.LabelRight}'");
                    }
                    paths.Add(new GraphPath(pathCondition.LabelRight, pathCondition.RolesRight));
                }
                return new GraphMatch(match.Unknown, paths[0], paths.Skip(1).ToImmutableList());
            }

            public GraphProjection? ConvertProjection(Projection projection)
            {
                switch (projection)
                {
                    case SimpleProjection _:
                    case FieldProjection _:
                    case HashProjection _:
                        // Read back from a label the product already carries.
                        return GraphProjection.NoCollections;

                    case SignerProjection signer:
                        return Refuse<GraphProjection>(
                            $"the projection reads the signers of '{signer.Tag}', which a fact graph does not carry");

                    case DateLearnedProjection dateLearned:
                        return Refuse<GraphProjection>(
                            $"the projection reads the date '{dateLearned.Tag}' was learned, which a fact graph does not carry");

                    case CollectionProjection collection:
                        // A collection that is not a direct child of a compound has no name
                        // to carry it, so neither this evaluator nor the store puts it in
                        // the product. What is inside it still has to be readable.
                        return ConvertCollection(collection) == null
                            ? null
                            : GraphProjection.NoCollections;

                    case CompoundProjection compound:
                        return ConvertCompound(compound);

                    default:
                        return Refuse<GraphProjection>(
                            $"the projection {projection.ToDescriptiveString()} has no graph form");
                }
            }

            private GraphProjection? ConvertCompound(CompoundProjection compound)
            {
                var collections = ImmutableDictionary.CreateBuilder<string, GraphCollection>();
                foreach (var name in compound.Names)
                {
                    var child = compound.GetProjection(name);
                    if (child is CollectionProjection childCollection)
                    {
                        var collection = ConvertCollection(childCollection);
                        if (collection == null)
                        {
                            return null;
                        }
                        collections.Add(name, collection);
                    }
                    else if (ConvertProjection(child) == null)
                    {
                        return null;
                    }
                }
                return new GraphProjection(collections.ToImmutable());
            }

            private GraphCollection? ConvertCollection(CollectionProjection collection)
            {
                var matches = ConvertMatches(collection.Matches);
                if (matches == null)
                {
                    return null;
                }
                var projection = ConvertProjection(collection.Projection);
                if (projection == null)
                {
                    return null;
                }
                return new GraphCollection(matches, projection);
            }
        }
    }

    /// <summary>
    /// An unknown the graph reaches by walking predecessors, along one path or several.
    /// The first path is a field of its own, so a match with no path cannot be built.
    /// </summary>
    internal sealed class GraphMatch
    {
        public GraphMatch(Label unknown, GraphPath path, ImmutableList<GraphPath> additionalPaths)
        {
            Unknown = unknown;
            Path = path;
            AdditionalPaths = additionalPaths;
        }

        public Label Unknown { get; }
        public GraphPath Path { get; }
        public ImmutableList<GraphPath> AdditionalPaths { get; }
    }

    /// <summary>
    /// Predecessor roles walked from a label already in scope. There is no left-hand side,
    /// so a successor step cannot be built.
    /// </summary>
    internal sealed class GraphPath
    {
        public GraphPath(string labelRight, ImmutableList<Role> roles)
        {
            LabelRight = labelRight;
            Roles = roles;
        }

        public string LabelRight { get; }
        public ImmutableList<Role> Roles { get; }
    }

    /// <summary>
    /// What a projection adds to a product: a named collection for each collection the
    /// projection names directly. Everything else is read back from the labels the product
    /// already carries, so it adds nothing here.
    /// </summary>
    internal sealed class GraphProjection
    {
        public static readonly GraphProjection NoCollections =
            new GraphProjection(ImmutableDictionary<string, GraphCollection>.Empty);

        private readonly ImmutableDictionary<string, GraphCollection> collections;

        public GraphProjection(ImmutableDictionary<string, GraphCollection> collections)
        {
            this.collections = collections;
        }

        public IEnumerable<string> Names => collections.Keys;

        public GraphCollection GetCollection(string name) => collections[name];
    }

    /// <summary>
    /// The matches and projection of one named collection.
    /// </summary>
    internal sealed class GraphCollection
    {
        public GraphCollection(ImmutableList<GraphMatch> matches, GraphProjection projection)
        {
            Matches = matches;
            Projection = projection;
        }

        public ImmutableList<GraphMatch> Matches { get; }
        public GraphProjection Projection { get; }
    }
}
