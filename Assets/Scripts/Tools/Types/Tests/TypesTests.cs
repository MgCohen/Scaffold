using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Scaffold.Types.Tests
{
    public class TypesTests
    {
        [Test]
        public void GetConstructorDependencies_SingleDep_ReturnsParameterTypes()
        {
            DependencyExtractor extractor = new DependencyExtractor();
            Type serviceType = typeof(ServiceWithDep);
            IEnumerable<Type> dependencies = extractor.GetConstructorDependencies(serviceType);
            Type[] types = dependencies.ToArray();
            Type expected = typeof(string);
            Assert.AreEqual(1, types.Length);
            Assert.AreEqual(expected, types[0]);
        }

        [Test]
        public void GetConstructorDependencies_NoDeps_ReturnsEmpty()
        {
            DependencyExtractor extractor = new DependencyExtractor();
            Type serviceType = typeof(ServiceNoDep);
            IEnumerable<Type> dependencies = extractor.GetConstructorDependencies(serviceType);
            Type[] types = dependencies.ToArray();
            Assert.AreEqual(0, types.Length);
        }

        [Test]
        public void GetConstructorDependencies_MultipleDeps_ReturnsAllParameterTypes()
        {
            DependencyExtractor extractor = new DependencyExtractor();
            Type serviceType = typeof(ServiceWithMultipleDeps);
            IEnumerable<Type> dependencies = extractor.GetConstructorDependencies(serviceType);
            Type[] types = dependencies.ToArray();
            Assert.AreEqual(2, types.Length);
        }

        private class ServiceWithDep
        {
            public ServiceWithDep(string dep) { }
        }

        private class ServiceNoDep
        {
        }

        private class ServiceWithMultipleDeps
        {
            public ServiceWithMultipleDeps(string dep1, int dep2) { }
        }
    }
}
