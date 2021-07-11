// using System;
// using System.Collections.Immutable;
// using System.Linq;
// using Jinaga.Pipelines;

// namespace Jinaga.Definitions
// {
//     public class StepsDefinition
//     {
//         private readonly string tag;
//         private readonly SetDefinition_Old? startingSet;
//         private readonly ImmutableList<Step> steps;

//         public string Tag => tag;

//         public string InitialFactName => startingSet == null
//             ? throw new InvalidOperationException("Using an uninitialized steps definition")
//             : startingSet.Tag;

//         public StepsDefinition(string tag, SetDefinition_Old? startingSet, ImmutableList<Step> steps)
//         {
//             this.tag = tag;
//             this.startingSet = startingSet;
//             this.steps = steps;
//         }

//         public StepsDefinition AddStep(Step step)
//         {
//             return new StepsDefinition(tag, startingSet, steps.Add(step));
//         }

//         public Pipeline CreatePipeline(string factType, ImmutableList<ConditionDefinition> conditions)
//         {
//             if (startingSet != null)
//             {
//                 var pipeline = startingSet.CreatePipeline();
//                 var allSteps = steps.AddRange(conditions.Select(condition => condition.CreateConditionalStep()));
//                 var path = new Path(tag, factType, startingSet.Tag, allSteps);
                
//                 return pipeline.WithPath(path);
//             }
//             else
//             {
//                 return Pipeline.FromInitialFact(tag, factType);
//             }
//         }
//     }
// }
