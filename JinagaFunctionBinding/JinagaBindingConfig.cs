using System;
using System.Collections.Generic;

namespace JinagaFunctionBinding
{
    public class JinagaBindingConfig
    {
        private readonly Dictionary<string, string> _specifications = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _startingPoints = new Dictionary<string, string>();

        public void RegisterSpecification(string name, string specification)
        {
            _specifications[name] = specification;
        }

        public string GetSpecification(string name)
        {
            return _specifications.TryGetValue(name, out var specification) ? specification : null;
        }

        public void RegisterStartingPoint(string name, string startingPoint)
        {
            _startingPoints[name] = startingPoint;
        }

        public string GetStartingPoint(string name)
        {
            return _startingPoints.TryGetValue(name, out var startingPoint) ? startingPoint : null;
        }
    }
}
