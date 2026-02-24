using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AutoPackerGenerator
{
    [Generator]
    public class AutoPackerGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor MustBeUnmanagedDiagnostic = new DiagnosticDescriptor(
            id: "CSG002",
            title: "Serialized fields must be unmanaged",
            messageFormat: "Field '{0}' must be an unmanaged type or define an unmanaged TargetType in [Packed]. Type '{1}' is managed.",
            category: "AutoPackerGenerator",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AutoPackSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is AutoPackSyntaxReceiver receiver))
                return;

            var validTypes = CollectAndEmitPartials(context, receiver);
            EmitRegistryFile(context, validTypes);
        }

        private static List<INamedTypeSymbol> CollectAndEmitPartials(GeneratorExecutionContext context, AutoPackSyntaxReceiver receiver)
        {
            var validTypes = new List<INamedTypeSymbol>();
            foreach (var pair in receiver.TypeFields)
            {
                var typeSymbol = pair.Key;
                var fields = pair.Value;

                if (fields.Count == 0)
                {
                    ReportNoFields(context, typeSymbol);
                    continue;
                }

                bool hasErrors = false;
                foreach (var tuple in fields)
                {
                    var fieldSymbol = tuple.Field;
                    var typeToCheck = tuple.TargetType ?? fieldSymbol.Type;
                    
                    if (!typeToCheck.IsUnmanagedType)
                    {
                        var location = fieldSymbol.Locations.Length > 0 ? fieldSymbol.Locations[0] : Location.None;
                        var diagnostic = Diagnostic.Create(MustBeUnmanagedDiagnostic, location, fieldSymbol.Name, typeToCheck.ToDisplayString());
                        context.ReportDiagnostic(diagnostic);
                        hasErrors = true;
                    }
                }

                if (hasErrors) continue;

                EmitPartial(context, typeSymbol, fields);
                validTypes.Add(typeSymbol);
            }
            return validTypes;
        }

        private static void EmitRegistryFile(GeneratorExecutionContext context, List<INamedTypeSymbol> types)
        {
            var source = Emitter.EmitRegistry(types);
            context.AddSource("AutoPackerRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        private static void ReportNoFields(GeneratorExecutionContext context, INamedTypeSymbol typeSymbol)
        {
            var location = typeSymbol.Locations.Length > 0 ? typeSymbol.Locations[0] : Location.None;
            //context.ReportDiagnostic(Diagnostic.Create(NoSerializedFieldsDiagnostic, location, typeSymbol.Name));
        }

        private static void EmitPartial(GeneratorExecutionContext context, INamedTypeSymbol typeSymbol, List<(IFieldSymbol Field, ITypeSymbol TargetType)> fields)
        {
            var source = Emitter.EmitSource(typeSymbol, fields);
            context.AddSource($"{typeSymbol.Name}.Packed.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }
}
