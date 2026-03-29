using System;
using System.Reflection;
using Scaffold.GraphFlow.Sample;

namespace Scaffold.GraphFlow
{
    public sealed partial class GraphRunner
    {
        static void WireInstance(ExecutableNode node, object instance, Flow flow)
        {
            foreach (var edge in node.DataEdgesIn)
            {
                if (!flow.LastInstanceByNode.TryGetValue(edge.SourceNode, out var sourceInst))
                    continue;

                TryCopyField(sourceInst, edge.SourcePortName, instance, edge.TargetPortName);
            }

            if (instance is AddNumbersInstance seedAdd)
            {
                seedAdd.A = flow.Blackboard.Get<int>("A");
                seedAdd.B = flow.Blackboard.Get<int>("B");
            }

            if (instance is MultiplyNumbersInstance seedMul)
            {
                seedMul.A = flow.Blackboard.Get<int>("A");
                seedMul.B = flow.Blackboard.Get<int>("B");
            }
        }

        static void TryCopyField(object source, string sourcePort, object target, string targetPort)
        {
            var sf = source.GetType().GetField(sourcePort, BindingFlags.Public | BindingFlags.Instance);
            var tf = target.GetType().GetField(targetPort, BindingFlags.Public | BindingFlags.Instance);
            if (sf == null || tf == null || tf.IsInitOnly)
                return;

            var v = sf.GetValue(source);
            if (v == null)
            {
                tf.SetValue(target, null);
                return;
            }

            if (tf.FieldType.IsAssignableFrom(v.GetType()))
            {
                tf.SetValue(target, v);
                return;
            }

            try
            {
                var converted = Convert.ChangeType(v, tf.FieldType);
                tf.SetValue(target, converted);
            }
            catch
            {
                // ignore incompatible edge
            }
        }
    }
}
