#nullable enable
using System;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor.Nodes
{
    /// <summary>
    /// Generic base for the per-package Return editor mirror. The concrete subclass is emitted by
    /// the package generator and closes <typeparamref name="TEnum"/> over the per-package
    /// <c>&lt;Stem&gt;Catalog.ReturnType</c> enum, then forwards <see cref="GetResultType"/> to
    /// <c>&lt;Stem&gt;Catalog.Resolve(choice).Type</c>.
    ///
    /// <para>Single dynamic data input named <c>Value</c> typed by the picked choice. Bake-side
    /// constructs the correct closed <c>Return&lt;TResult&gt;</c> via the catalog's parameterless
    /// factory.</para>
    /// </summary>
    [Serializable]
    public abstract class ReturnEditorNode<TEnum> : Node where TEnum : struct, Enum
    {
        public const string FlowInPortName       = "In";
        public const string ValuePortName        = "Value";
        public const string ResultTypeOptionName = "ResultType";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<TEnum>(ResultTypeOptionName)
                .WithDisplayName("Result Type")
                .WithTooltip("Pick the type this Return terminator writes into the run's Result.");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort(FlowInPortName)
                .WithDisplayName(string.Empty)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            var opt = GetNodeOptionByName(ResultTypeOptionName);
            if (opt == null) return;
            if (!opt.TryGetValue<TEnum>(out var picked)) return;

            var resultType = GetResultType(picked);
            if (resultType == null) return;

            AddValuePortByType(context, ValuePortName, resultType);
        }

        /// <summary>Provides the <see cref="Type"/> of the picked result-type choice. Per-package
        /// shim implements via <c>&lt;Stem&gt;Catalog.Resolve(picked)?.Type</c>.</summary>
        protected abstract Type? GetResultType(TEnum picked);

        static void AddValuePortByType(IPortDefinitionContext context, string name, Type t)
        {
            if (t == typeof(int))         context.AddInputPort<int>(name).Build();
            else if (t == typeof(string)) context.AddInputPort<string>(name).Build();
            else if (t == typeof(bool))   context.AddInputPort<bool>(name).Build();
            else if (t == typeof(float))  context.AddInputPort<float>(name).Build();
            else                          context.AddInputPort(name).Build();
        }
    }
}
