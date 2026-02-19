using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ObservableNestedPropertiesGenerator
{
    internal static class Symbols
    {
        public const string ObservableAttribute = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
        public const string ObservableNestedAttribute = "NestedObservableObjectAttribute";
        public const string NestedPropertyAttribute = "MVVM.Binding.NestedPropertyAttribute";
        public const string ObservableNestedInterface = "INestedObservableProperties";

        public static string GetUsingStatements()
        {
            return $@"
using System.ComponentModel;
using System.Collections;
using Scaffold.MVVM.Binding;
";
        }

        public static string GetSourceClassBody(INamedTypeSymbol classSymbol)
        {
            return $@"
public partial class {classSymbol.Name} : {Symbols.ObservableNestedInterface}
{{

    protected void RegisterChildProperty(string objName, object obj)
    {{
        if(obj is INotifyPropertyChanged npc)
        {{
            npc.PropertyChanged += (s, p) => RaiseChildProperty(objName, obj, s, p);
        }}

        if(obj is {Symbols.ObservableNestedInterface} onp)
        {{
            onp.RegisterNestedProperties();
        }}
    }}

    private void RaiseChildProperty(string objName, object parent, object sender, PropertyChangedEventArgs args)
    {{
        string separator = parent is IEnumerable && args.PropertyName.StartsWith('[') ? """" : ""."";
        string nestedPropertyPath = string.Join(separator, objName, args.PropertyName);
        OnPropertyChanged(nestedPropertyPath);
        //OnPropertyChanged(objName);
    }}

    public virtual void RegisterNestedProperties()
    {{
";
        }

        public static string GetChildClassBody(INamedTypeSymbol classSymbol)
        {
            return $@"
public partial class {classSymbol.Name} 
{{
    public override void RegisterNestedProperties()
    {{
        base.RegisterNestedProperties();
";
        }

        public static string GetNamespaceDefinition(INamedTypeSymbol classSymbol)
        {
            return $@"
namespace {classSymbol.ContainingNamespace.ToDisplayString()}
{{
";
        }
    }

    [Generator]
    public class ObservableParentGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ObservablesSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxContextReceiver is ObservablesSyntaxReceiver receiver))
                return;

            var classGroups = receiver.Fields.GroupBy<IFieldSymbol, INamedTypeSymbol>(f => f.ContainingType, SymbolEqualityComparer.Default);
            var baseClasses = receiver.Classes.Except(classGroups.Select(cg => cg.Key));

            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in classGroups)
            {
                var classSource = ProcessClass(group.Key, group);
                context.AddSource($"{group.Key.Name}_nested.g.cs", SourceText.From(classSource, Encoding.UTF8));
            }

            foreach (var baseClass in baseClasses)
            {
                var classSource = ProcessClass(baseClass as INamedTypeSymbol, Enumerable.Empty<IFieldSymbol>());
                context.AddSource($"{baseClass.Name}_nested.g.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }

        private string ProcessClass(INamedTypeSymbol classSymbol, IEnumerable<IFieldSymbol> fields)
        {
            //TODO check if we dont have nested attributes, as we only need to do this on the lowest level containing the attribute
            //TODO we should probably also check for direct implementations
            bool isNestedSource = classSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals(Symbols.ObservableNestedAttribute));
            bool hasNamespace = classSymbol.ContainingNamespace.Name.Length > 0; //to avoid Global namespace weirdness
            var source = new StringBuilder();
            source.AppendLine(Symbols.GetUsingStatements());
            if (hasNamespace)
            {
                source.AppendLine(Symbols.GetNamespaceDefinition(classSymbol));
            }

            if (isNestedSource)
            {
                source.AppendLine(Symbols.GetSourceClassBody(classSymbol));
            }
            else
            {
                source.AppendLine(Symbols.GetChildClassBody(classSymbol));
            }

            foreach (IFieldSymbol fieldSymbol in fields)
            {
                ProcessField(source, fieldSymbol);
            }
            source.AppendLine("    }\n\n}");
            if (hasNamespace)
            {
                source.AppendLine("}");
            }
            return source.ToString();
        }

        private void ProcessField(StringBuilder source, IFieldSymbol fieldSymbol)
        {
            bool changeFieldName = fieldSymbol.GetAttributes().Any(ad => ad.AttributeClass.ToDisplayString() == Symbols.ObservableAttribute);
            var fieldName = changeFieldName ? ToPascalCase(fieldSymbol.Name) : fieldSymbol.Name;
            source.AppendLine($@"        RegisterChildProperty(""{fieldName}"",{fieldName});");
        }

        private static string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove leading underscores and @ symbols
            input = input.TrimStart('_', '@');
            
            if (string.IsNullOrEmpty(input))
                return input;

            // Split by underscores if present
            var parts = input.Split('_');
            var result = new StringBuilder();
            
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;
                    
                // Capitalize first letter and lowercase the rest
                if (part.Length > 0)
                {
                    result.Append(char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                    {
                        result.Append(part.Substring(1));
                    }
                }
            }
            
            return result.ToString();
        }
    }


    internal class ObservablesSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IFieldSymbol> Fields { get; } = new List<IFieldSymbol>();
        public List<ISymbol> Classes { get; } = new List<ISymbol>();

        private readonly IEnumerable<string> FieldAttributes = new List<string>()
        {
            Symbols.ObservableAttribute,
            Symbols.NestedPropertyAttribute
        };

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is FieldDeclarationSyntax fieldDeclarationSyntax && fieldDeclarationSyntax.AttributeLists.Count > 0)
            {
                foreach (VariableDeclaratorSyntax variable in fieldDeclarationSyntax.Declaration.Variables)
                {
                    IFieldSymbol fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                    
                    //check if field is inside a INestedObservableProperties
                    if (!BelongsToNestedObservable(fieldSymbol))
                    {
                        continue;
                    }

                    //check if field is either ObservableProperty or NestedProperty
                    var attributeNames = fieldSymbol.GetAttributes().Select(ad => ad.AttributeClass.ToDisplayString());
                    if (attributeNames.Any(s => FieldAttributes.Contains(s)))
                    {
                        Fields.Add(fieldSymbol);
                    }
                }
            }

            if (context.Node is ClassDeclarationSyntax classNode)
            {
                ISymbol classSymbol = context.SemanticModel.GetDeclaredSymbol(classNode);
                if (classSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals(Symbols.ObservableNestedAttribute)))
                {
                    Classes.Add(classSymbol);
                }
            }
        }

        private bool BelongsToNestedObservable(IFieldSymbol symbol)
        {
            var typeSymbol = symbol.ContainingType;
            while (typeSymbol != null)
            {
                if (typeSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals(Symbols.ObservableNestedAttribute)))
                {
                    return true;
                }
                typeSymbol = typeSymbol.BaseType;
            }
            return false;
        }

    }
}