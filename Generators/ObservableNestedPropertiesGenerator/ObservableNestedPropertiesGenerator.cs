using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ObservableNestedPropertiesGenerator
{
    internal sealed class TrackedFieldInfo
    {
        public TrackedFieldInfo(string propertyName, string typeName, bool isObservableProperty)
        {
            PropertyName = propertyName;
            TypeName = typeName;
            IsObservableProperty = isObservableProperty;
        }

        public string PropertyName { get; }
        public string TypeName { get; }
        public bool IsObservableProperty { get; }
        public string SafeName => PropertyName.Replace(".", string.Empty);
    }

    internal static class Symbols
    {
        public const string ObservableAttribute = "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
        public const string ObservableNestedAttribute = "NestedObservableObjectAttribute";
        public const string NestedPropertyAttribute = "Scaffold.MVVM.Binding.NestedPropertyAttribute";
        public const string ObservableNestedInterface = "INestedObservableProperties";
        public const string BindSourceAttribute = "BindSourceAttribute";
        public const string BindingsInterface = "Scaffold.MVVM.Binding.IBindings";
        public const string BindSourceInterface = "Scaffold.MVVM.Binding.IBindSource";

        public static string GetNestedUsingStatements()
        {
            return $@"
using System.ComponentModel;
using System.Collections;
using Scaffold.MVVM.Binding;
";
        }

        public static string GetNestedSourceClassBody(INamedTypeSymbol classSymbol)
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

    protected void RefreshChildProperty(string objName, object obj, ref object observedChild, ref PropertyChangedEventHandler propertyChangedHandler)
    {{
        if (propertyChangedHandler != null && observedChild is INotifyPropertyChanged previousObservable)
        {{
            previousObservable.PropertyChanged -= propertyChangedHandler;
        }}

        observedChild = obj;
        propertyChangedHandler = null;

        if (obj is INotifyPropertyChanged currentObservable)
        {{
            propertyChangedHandler = (s, p) => RaiseChildProperty(objName, obj, s, p);
            currentObservable.PropertyChanged += propertyChangedHandler;
        }}

        if (obj is {Symbols.ObservableNestedInterface} nestedObservable)
        {{
            nestedObservable.RegisterNestedProperties();
        }}
    }}

    private void RaiseChildProperty(string objName, object parent, object sender, PropertyChangedEventArgs args)
    {{
        string childPropertyName = args?.PropertyName ?? string.Empty;
        string separator = parent is IEnumerable && childPropertyName.StartsWith('[') ? """" : ""."";
        string nestedPropertyPath = string.Join(separator, objName, childPropertyName);
        OnPropertyChanged(nestedPropertyPath);
        //OnPropertyChanged(objName);
    }}

    public virtual void RegisterNestedProperties()
    {{
";
        }

        public static string GetNestedChildClassBody(INamedTypeSymbol classSymbol)
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

        public static string GetBindSourceUsingStatements()
        {
            return @"
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Scaffold.MVVM.Binding;
";
        }

        public static string GetBindSourceClassBody(INamedTypeSymbol classSymbol, string bindingType, bool shouldImplementBindSource)
        {
            var bindSourceClause = shouldImplementBindSource
                ? $" : {Symbols.BindSourceInterface}"
                : string.Empty;

            return $@"
public partial class {classSymbol.Name}{bindSourceClause}
{{
    private readonly {Symbols.BindingsInterface} _bindSourceBindings = new {bindingType}();

    protected {Symbols.BindingsInterface} bindings => _bindSourceBindings;

    public IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Expression<Func<TTarget>> target, BindingOptions options = null)
    {{
        return _bindSourceBindings.RegisterBind(source, target, options);
    }}

    public IBindedProperty<TSource, TTarget> Bind<TSource, TTarget>(Expression<Func<TSource>> source, Action<TTarget> target, BindingOptions options = null)
    {{
        return _bindSourceBindings.RegisterBind(source, target, options);
    }}

    public void BindConverter<TSource, TTarget>(Func<TSource, TTarget> converter)
    {{
        GenericConverter<TSource, TTarget> genericConverter = new GenericConverter<TSource, TTarget>(converter);
        BindConverter(genericConverter);
    }}

    public void BindConverter<TSource, TTarget>(Scaffold.MVVM.Binding.Converter<TSource, TTarget> converter)
    {{
        _bindSourceBindings.RegisterConverter(converter);
    }}

    public IBindedCollection<TSource, TTarget> BindCollection<TSource, TTarget>(Expression<Func<ICollection<TSource>>> source, ICollectionHandler<TSource, TTarget> handler, BindingOptions options = null)
    {{
        return _bindSourceBindings.RegisterBindCollection(source, handler, options);
    }}

    public void UpdateBinding(string bindKey)
    {{
        _bindSourceBindings.UpdateBind(bindKey);
    }}

    public void ClearBindings()
    {{
        _bindSourceBindings.Unbind();
    }}
}}
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
            var baseClasses = receiver.Classes.Where(c => !classGroups.Any(g => SymbolEqualityComparer.Default.Equals(g.Key, c)));

            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in classGroups)
            {
                var classSource = ProcessNestedClass(group.Key, group);
                context.AddSource($"{group.Key.Name}_nested.g.cs", SourceText.From(classSource, Encoding.UTF8));
            }

            foreach (var baseClass in baseClasses)
            {
                var classSource = ProcessNestedClass(baseClass, Enumerable.Empty<IFieldSymbol>());
                context.AddSource($"{baseClass.Name}_nested.g.cs", SourceText.From(classSource, Encoding.UTF8));
            }

            List<INamedTypeSymbol> bindSources = new List<INamedTypeSymbol>();
            foreach (var bindSourceClass in receiver.BindSourceClasses)
            {
                if (bindSources.Any(existing => SymbolEqualityComparer.Default.Equals(existing, bindSourceClass)))
                {
                    continue;
                }

                bindSources.Add(bindSourceClass);
            }

            foreach (var bindSource in bindSources)
            {
                var bindingType = GetBindingType(bindSource);
                if (bindingType == null)
                {
                    continue;
                }

                var classSource = ProcessBindSourceClass(bindSource, bindingType);
                context.AddSource($"{bindSource.Name}_bindsource.g.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }

        private string ProcessNestedClass(INamedTypeSymbol classSymbol, IEnumerable<IFieldSymbol> fields)
        {
            //TODO check if we dont have nested attributes, as we only need to do this on the lowest level containing the attribute
            //TODO we should probably also check for direct implementations
            List<TrackedFieldInfo> trackedFields = fields.Select(MapTrackedField).ToList();
            bool isNestedSource = classSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals(Symbols.ObservableNestedAttribute));
            bool hasNamespace = classSymbol.ContainingNamespace.Name.Length > 0; //to avoid Global namespace weirdness
            var source = new StringBuilder();
            source.AppendLine(Symbols.GetNestedUsingStatements());
            if (hasNamespace)
            {
                source.AppendLine(Symbols.GetNamespaceDefinition(classSymbol));
            }

            if (isNestedSource)
            {
                source.AppendLine(Symbols.GetNestedSourceClassBody(classSymbol));
            }
            else
            {
                source.AppendLine(Symbols.GetNestedChildClassBody(classSymbol));
            }

            foreach (TrackedFieldInfo field in trackedFields)
            {
                source.AppendLine($@"        RefreshChildProperty(""{field.PropertyName}"", {field.PropertyName}, ref __{field.SafeName}ObservedChild, ref __{field.SafeName}PropertyChangedHandler);");
            }

            List<TrackedFieldInfo> observableTrackedFields = trackedFields.Where(field => field.IsObservableProperty).ToList();
            if (observableTrackedFields.Count > 0)
            {
                source.AppendLine();
                source.AppendLine("        EnsureNestedRefreshHandlerRegistered();");
            }

            source.AppendLine("    }");

            foreach (TrackedFieldInfo field in trackedFields)
            {
                source.AppendLine();
                source.AppendLine($@"    private object __{field.SafeName}ObservedChild;");
                source.AppendLine($@"    private PropertyChangedEventHandler __{field.SafeName}PropertyChangedHandler;");
            }

            if (observableTrackedFields.Count > 0)
            {
                source.AppendLine();
                source.AppendLine("    private bool __nestedRefreshHandlerRegistered;");
                source.AppendLine("    private PropertyChangedEventHandler __nestedRefreshHandler;");
                source.AppendLine();
                source.AppendLine("    private void EnsureNestedRefreshHandlerRegistered()");
                source.AppendLine("    {");
                source.AppendLine("        if (__nestedRefreshHandlerRegistered)");
                source.AppendLine("        {");
                source.AppendLine("            return;");
                source.AppendLine("        }");
                source.AppendLine();
                source.AppendLine("        __nestedRefreshHandler = HandleNestedRefreshPropertyChanged;");
                source.AppendLine("        PropertyChanged += __nestedRefreshHandler;");
                source.AppendLine("        __nestedRefreshHandlerRegistered = true;");
                source.AppendLine("    }");
                source.AppendLine();
                source.AppendLine("    private void HandleNestedRefreshPropertyChanged(object sender, PropertyChangedEventArgs args)");
                source.AppendLine("    {");
                source.AppendLine("        if (string.IsNullOrEmpty(args?.PropertyName))");
                source.AppendLine("        {");
                source.AppendLine("            return;");
                source.AppendLine("        }");
                source.AppendLine();
                source.AppendLine("        switch (args.PropertyName)");
                source.AppendLine("        {");
                foreach (TrackedFieldInfo field in observableTrackedFields)
                {
                    source.AppendLine($@"            case nameof({field.PropertyName}):");
                    source.AppendLine($@"                RefreshChildProperty(""{field.PropertyName}"", {field.PropertyName}, ref __{field.SafeName}ObservedChild, ref __{field.SafeName}PropertyChangedHandler);");
                    source.AppendLine("                break;");
                }
                source.AppendLine("        }");
                source.AppendLine("    }");
            }

            source.AppendLine();
            source.AppendLine("}");
            if (hasNamespace)
            {
                source.AppendLine("}");
            }
            return source.ToString();
        }

        private string ProcessBindSourceClass(INamedTypeSymbol classSymbol, INamedTypeSymbol bindingType)
        {
            bool hasNamespace = classSymbol.ContainingNamespace.Name.Length > 0;
            string bindingTypeName = bindingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            bool implementsBindSource = classSymbol.AllInterfaces
                .Any(i => i.ToDisplayString() == Symbols.BindSourceInterface);
            bool shouldImplementBindSource = !implementsBindSource;
            var source = new StringBuilder();
            source.AppendLine(Symbols.GetBindSourceUsingStatements());
            if (hasNamespace)
            {
                source.AppendLine(Symbols.GetNamespaceDefinition(classSymbol));
            }

            source.AppendLine(Symbols.GetBindSourceClassBody(classSymbol, bindingTypeName, shouldImplementBindSource));

            if (hasNamespace)
            {
                source.AppendLine("}");
            }

            return source.ToString();
        }

        private TrackedFieldInfo MapTrackedField(IFieldSymbol fieldSymbol)
        {
            bool isObservableProperty = fieldSymbol.GetAttributes().Any(ad => ad.AttributeClass.ToDisplayString() == Symbols.ObservableAttribute);
            string propertyName = isObservableProperty ? ToPascalCase(fieldSymbol.Name) : fieldSymbol.Name;
            string typeName = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return new TrackedFieldInfo(propertyName, typeName, isObservableProperty);
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

        private static INamedTypeSymbol GetBindingType(INamedTypeSymbol classSymbol)
        {
            var bindSourceAttribute = classSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name.Equals(Symbols.BindSourceAttribute) == true);

            if (bindSourceAttribute == null || bindSourceAttribute.ConstructorArguments.Length != 1)
            {
                return null;
            }

            var firstArgument = bindSourceAttribute.ConstructorArguments[0];
            if (firstArgument.Kind != TypedConstantKind.Type)
            {
                return null;
            }

            return firstArgument.Value as INamedTypeSymbol;
        }
    }


    internal class ObservablesSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<IFieldSymbol> Fields { get; } = new List<IFieldSymbol>();
        public List<INamedTypeSymbol> Classes { get; } = new List<INamedTypeSymbol>();
        public List<INamedTypeSymbol> BindSourceClasses { get; } = new List<INamedTypeSymbol>();

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
                var classSymbol = context.SemanticModel.GetDeclaredSymbol(classNode) as INamedTypeSymbol;
                if (classSymbol == null)
                {
                    return;
                }

                if (classSymbol.GetAttributes().Any(a => a.AttributeClass.Name.Equals(Symbols.ObservableNestedAttribute)))
                {
                    Classes.Add(classSymbol);
                }

                if (classSymbol.GetAttributes().Any(a => a.AttributeClass?.Name.Equals(Symbols.BindSourceAttribute) == true))
                {
                    BindSourceClasses.Add(classSymbol);
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
