using System;
using System.Collections.Immutable;
using System.Linq;
using Jinaga.Definitions;
using Jinaga.Parsers;
using Jinaga.Pipelines;
using Jinaga.Projections;

namespace Jinaga.Generators
{
    public static class SpecificationGenerator
    {
        public static SpecificationOld CreateSpecification(SpecificationContext context, SpecificationResult result)
        {
            var pipeline = PipelineGenerator.CreatePipeline(context, result);
            var projection = CreateProjection(result.SymbolValue);
            return new SpecificationOld(pipeline, projection);
        }

        private static ProjectionOld CreateProjection(SymbolValue value)
        {
            if (value is SymbolValueSetDefinition setDefinitionValue)
            {
                return new SimpleProjection(setDefinitionValue.SetDefinition.Tag);
            }
            else if (value is SymbolValueComposite compositeValue)
            {
                var projectionDefinition = compositeValue.CreateProjectionDefinition();
                var projection = projectionDefinition
                    .AllTags()
                    .Aggregate(new CompoundProjection(), (p, tag) => p.With(
                        tag,
                        CreateProjection(projectionDefinition.GetValue(tag))));
                return projection;
            }
            else if (value is SymbolValueCollection collectionValue)
            {
                return new CollectionProjection(collectionValue.Specification);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static ImmutableList<Match> CreateMatches(SpecificationContext context, SpecificationResult result)
        {
            var matches = result.SetDefinitions
                .Select(setDefinition => CreateMatch(context, setDefinition, result))
                .ToImmutableList();
            return matches;
        }

        private static Match CreateMatch(SpecificationContext context, SetDefinition setDefinition, SpecificationResult result)
        {
            if (setDefinition is SetDefinitionPredecessorChain predecessorChainSet)
            {
                throw new NotImplementedException();

            }
            else if (setDefinition is SetDefinitionJoin joinSet)
            {
                var head = joinSet.Head;
                var tail = joinSet.Tail;
                var sourceSetDefinition = head.TargetSetDefinition;
                var source = sourceSetDefinition.Label;
                var target = joinSet.Label;
                ImmutableList<Role> rolesLeft = AddSuccessorSteps(ImmutableList<Role>.Empty, tail);
                ImmutableList<Role> rolesRight = AddPredecessorSteps(ImmutableList<Role>.Empty, head);
                var condition = (MatchCondition)new PathCondition(rolesLeft, source.Name, rolesRight);
                var conditions = ImmutableList.Create(condition);
                var match = new Match(target, conditions);
                return match;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static ImmutableList<Role> AddSuccessorSteps(ImmutableList<Role> roles, Chain chain)
        {
            if (chain is ChainRole chainRole)
            {
                return AddSuccessorSteps(roles, chainRole.Prior)
                    .Add(new Role(chainRole.Role, chainRole.TargetFactType));
            }
            else
            {
                return roles;
            }
        }

        private static ImmutableList<Role> AddPredecessorSteps(ImmutableList<Role> roles, Chain chain)
        {
            if (chain is ChainRole chainRole)
            {
                return AddPredecessorSteps(roles, chainRole.Prior)
                    .Add(new Role(chainRole.Role, chain.TargetFactType));
            }
            else
            {
                return roles;
            }
        }
    }
}
