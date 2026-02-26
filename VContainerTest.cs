using System;
using System.Linq;
using VContainer;
using VContainer.Internal;

public class VContainerDependencyAnalyzer
{
    public static void AnalyzeDependencies(Type type)
    {
        var injectTypeInfo = TypeAnalyzer.Analyze(type);
        if (injectTypeInfo.InjectConstructor != null)
        {
            var ctor = injectTypeInfo.InjectConstructor;
            var parameters = ctor.ParameterInfos;
            Console.WriteLine($"Type {type.Name} has {parameters.Length} dependencies:");
            foreach (var p in parameters)
            {
                Console.WriteLine($"  - {p.ParameterType.Name} {p.Name}");
            }
        }
        else
        {
            Console.WriteLine($"Type {type.Name} has no registered constructor/dependencies.");
        }
    }
}
