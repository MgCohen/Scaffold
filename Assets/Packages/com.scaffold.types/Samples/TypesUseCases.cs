using System;
using System.Collections.Generic;
using System.Linq;

namespace Scaffold.Types.Samples
{
    public class TypesUseCases
    {
        public Type UseCaseExtractSingleConstructorDependency()
        {
            DependencyExtractor extractor = new DependencyExtractor();
            Type serviceType = typeof(ServiceWithDep);
            IEnumerable<Type> dependencies = extractor.GetConstructorDependencies(serviceType);
            return dependencies.First();
        }

        public bool UseCaseVerifyNoDependenciesOnDefaultService()
        {
            DependencyExtractor extractor = new DependencyExtractor();
            Type serviceType = typeof(ServiceNoDep);
            IEnumerable<Type> dependencies = extractor.GetConstructorDependencies(serviceType);
            Type[] types = dependencies.ToArray();
            return types.Length == 0;
        }

        private class ServiceWithDep
        {
            public ServiceWithDep(string dep) { }
        }

        private class ServiceNoDep
        {
        }
    }
}


