using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow.Generated;
using Scaffold.GraphFlow.Sample;
using UnityEngine;

namespace Scaffold.GraphFlow.Tests
{
    public sealed class GraphFlowRunnerTests
    {
        [Test]
        public void CentralService_Bootstrap_DoesNotThrow()
        {
            var central = new GraphFlowCentralService();
            central.RegisterGraph(new NoopGraphFlowObject());
            var registry = CreateRegistry();
            central.Bootstrap(registry);
        }

        sealed class NoopGraphFlowObject : IGraphFlowObject
        {
            public void Initialize(
                IReadOnlyList<IGraphMiddleware> middlewares,
                INodeExecutorRegistry registry,
                IGraphTickService tickService)
            {
            }
        }

        [Test]
        public async Task Linear_Add_Then_Log_Wires_Sum_To_Message()
        {
            LogObjectDefinition.ResetLastLoggedForTests();

            var addId = Guid.NewGuid().ToString();
            var logId = Guid.NewGuid().ToString();
            var main = ScriptableObject.CreateInstance<RuntimeGraph>();
            main.ClearSerializedForTests();
            main.AppendSerializedNode(addId, new AddNumbersDefinition().DefinitionTypeId);
            main.AppendSerializedNode(logId, new LogObjectDefinition().DefinitionTypeId);
            main.AppendSerializedEdge(addId, "Sum", "Message", logId);
            main.AppendSerializedEdge(addId, "Out", "In", logId);
            main.AppendSerializedEntry(typeof(GameStartEntry).AssemblyQualifiedName, addId);

            var registry = CreateRegistry();
            var exec = main.BuildExecutable(registry);
            var runner = new GraphRunner(Array.Empty<IGraphMiddleware>(), registry);
            var flow = new Flow(CancellationToken.None);
            flow.Blackboard.Set("A", 1);
            flow.Blackboard.Set("B", 1);

            await runner.RunAsync<GameStartEntry>(exec, flow, CancellationToken.None);

            Assert.AreEqual(2, LogObjectDefinition.LastLoggedForTests);
        }

        [Test]
        public async Task InvokeSubGraph_Runs_Nested_Add_Then_Log()
        {
            LogObjectDefinition.ResetLastLoggedForTests();

            var nestedAddId = Guid.NewGuid().ToString();
            var nestedLogId = Guid.NewGuid().ToString();
            var nested = ScriptableObject.CreateInstance<RuntimeGraph>();
            nested.ClearSerializedForTests();
            nested.AppendSerializedNode(nestedAddId, new AddNumbersDefinition().DefinitionTypeId);
            nested.AppendSerializedNode(nestedLogId, new LogObjectDefinition().DefinitionTypeId);
            nested.AppendSerializedEdge(nestedAddId, "Sum", "Message", nestedLogId);
            nested.AppendSerializedEdge(nestedAddId, "Out", "In", nestedLogId);
            nested.AppendSerializedEntry(typeof(SubGraphEntry).AssemblyQualifiedName, nestedAddId);

            var invokeId = Guid.NewGuid().ToString();
            var addId = Guid.NewGuid().ToString();
            var logId = Guid.NewGuid().ToString();
            var main = ScriptableObject.CreateInstance<RuntimeGraph>();
            main.ClearSerializedForTests();
            main.AppendSerializedNode(invokeId, new InvokeSubGraphDefinition().DefinitionTypeId, nested);
            main.AppendSerializedNode(addId, new AddNumbersDefinition().DefinitionTypeId);
            main.AppendSerializedNode(logId, new LogObjectDefinition().DefinitionTypeId);
            main.AppendSerializedEdge(invokeId, "Out", "In", addId);
            main.AppendSerializedEdge(addId, "Out", "In", logId);
            main.AppendSerializedEdge(addId, "Sum", "Message", logId);
            main.AppendSerializedEntry(typeof(GameStartEntry).AssemblyQualifiedName, invokeId);

            var registry = CreateRegistry();
            var exec = main.BuildExecutable(registry);
            var runner = new GraphRunner(Array.Empty<IGraphMiddleware>(), registry);
            var flow = new Flow(CancellationToken.None);
            flow.Blackboard.Set("A", 1);
            flow.Blackboard.Set("B", 1);

            await runner.RunAsync<GameStartEntry>(exec, flow, CancellationToken.None);

            Assert.AreEqual(2, LogObjectDefinition.LastLoggedForTests);
        }

        [Test]
        public async Task Add_Then_Multiply_Wires_Sum_To_Both_Inputs()
        {
            LogObjectDefinition.ResetLastLoggedForTests();

            var addId = Guid.NewGuid().ToString();
            var mulId = Guid.NewGuid().ToString();
            var logId = Guid.NewGuid().ToString();
            var main = ScriptableObject.CreateInstance<RuntimeGraph>();
            main.ClearSerializedForTests();
            main.AppendSerializedNode(addId, new AddNumbersDefinition().DefinitionTypeId);
            main.AppendSerializedNode(mulId, new MultiplyNumbersDefinition().DefinitionTypeId);
            main.AppendSerializedNode(logId, new LogObjectDefinition().DefinitionTypeId);
            main.AppendSerializedEdge(addId, "Sum", "A", mulId);
            main.AppendSerializedEdge(addId, "Sum", "B", mulId);
            main.AppendSerializedEdge(addId, "Out", "In", mulId);
            main.AppendSerializedEdge(mulId, "Product", "Message", logId);
            main.AppendSerializedEdge(mulId, "Out", "In", logId);
            main.AppendSerializedEntry(typeof(GameStartEntry).AssemblyQualifiedName, addId);

            var registry = CreateRegistry();
            var exec = main.BuildExecutable(registry);
            var runner = new GraphRunner(Array.Empty<IGraphMiddleware>(), registry);
            var flow = new Flow(CancellationToken.None);
            flow.Blackboard.Set("A", 1);
            flow.Blackboard.Set("B", 1);

            await runner.RunAsync<GameStartEntry>(exec, flow, CancellationToken.None);

            Assert.AreEqual(4, LogObjectDefinition.LastLoggedForTests);
        }

        static GraphFlowRegistry CreateRegistry()
        {
            var r = new GraphFlowRegistry();
            GraphFlowGeneratedRegistration.RegisterAll(r);
            return r;
        }
    }
}
