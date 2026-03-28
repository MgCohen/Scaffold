using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.GraphFlow.Sample
{
    public sealed class LogObjectInstance
    {
        public object Message;
    }

    public sealed class LogObjectDefinition : GraphNodeDefinitionBase<LogObjectInstance>
    {
        public InputConnection<object> Message;
        public FlowInput In;
        public FlowOutput Out;

        public static object LastLoggedForTests { get; private set; }

        public override string DefinitionTypeId => GraphFlowDefinitionIds.LogObject;

        protected override ValueTask ExecuteAsync(LogObjectInstance instance, Flow flow, CancellationToken cancellationToken)
        {
            LastLoggedForTests = instance.Message;
            Debug.Log(instance.Message?.ToString() ?? "null");
            return default;
        }
    }
}
