using System;
using Jinaga.Projections;

namespace Jinaga.Pipelines
{
    internal class SpecificationEdge
    {
        private string labelLeft;
        private PathCondition pathCondition;

        public SpecificationEdge(string name, PathCondition pathCondition)
        {
            this.labelLeft = name;
            this.pathCondition = pathCondition;
        }

        public bool ConnectsWith(string label)
        {
            return labelLeft == label || pathCondition.LabelRight == label;
        }

        public PathCondition WithLeft(string label)
        {
            if (labelLeft == label)
            {
                // The path is already in the correct order.
                return pathCondition;
            }
            else if (pathCondition.LabelRight == label)
            {
                // Swap the left and right labels.
                return new PathCondition(
                    pathCondition.RolesRight,
                    labelLeft,
                    pathCondition.RolesLeft
                );
            }
            else
            {
                throw new InvalidOperationException($"Cannot connect edge with label {label}");
            }
        }

        public string OtherLabel(string label)
        {
            if (labelLeft == label)
            {
                return pathCondition.LabelRight;
            }
            else if (pathCondition.LabelRight == label)
            {
                return labelLeft;
            }
            else
            {
                throw new InvalidOperationException($"Cannot find other label for edge with label {label}");
            }
        }
    }
}