using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
            LogObjectDefinition.LastLoggedForTests = null;

            var addId = Guid.NewGuid().ToString();
            var logId = Guid.NewGuid().ToString();
            var main = ScriptableObject.CreateInstance<RuntimeGraph>();
            main.ClearSerializedForTests();
            main.AppendSerializedNode(addId, GraphFlowDefinitionIds.AddNumbers);
            main.AppendSerializedNode(logId, GraphFlowDefinitionIds.LogObject);
            main.AppendSerializedEdge(addId, "Sum", "Message", logId);
            main.AppendSerializedEdge(addId, "Out", "In", logId);
            main.AppendSerializedEntry(typeof(GameStartEntry).AssemblyQualifiedName, addId);

            var registry = CreateRegistry();
            var exec = main.BuildExecutable(registry);
            var runner = new GraphRunner(Array.Empty<IGraphMiddleware>());
            var flow = new Flow(CancellationToken.None);
            flow.Blackboard.Set("A", 1);
            flow.Blackboard.Set("B", 1);

            await runner.RunAsync<GameStartEntry>(exec, flow, CancellationToken.None);

            Assert.AreEqual(2, LogObjectDefinition.LastLoggedForTests);
        }

        [Test]
        public async Task Reactive_Before_Add_Doubles_Then_Add_Produces_Four()
        {
            LogObjectDefinition.LastLoggedForTests = null;

            var addId = Guid.NewGuid().ToString();
            var logId = Guid.NewGuid().ToString();
            var multId = Guid.NewGuid().ToString();

            var reactive = ScriptableObject.CreateInstance<RuntimeGraph>();
            reactive.ClearSerializedForTests();
            reactive.AppendSerializedNode(multId, GraphFlowDefinitionIds.MultiplyAddInputs);
            reactive.AppendSerializedEntry(typeof(ReactiveChildEntry).AssemblyQualifiedName, multId);

            var main = ScriptableObject.CreateInstance<RuntimeGraph>();
            main.ClearSerializedForTests();
            main.AppendSerializedNode(addId, GraphFlowDefinitionIds.AddNumbers);
            main.AppendSerializedNode(logId, GraphFlowDefinitionIds.LogObject);
            main.AppendSerializedEdge(addId, "Sum", "Message", logId);
            main.AppendSerializedEdge(addId, "Out", "In", logId);
            main.AppendSerializedEntry(typeof(GameStartEntry).AssemblyQualifiedName, addId);
            main.AppendSerializedReactiveHook(MiddlewarePhase.Before, GraphFlowDefinitionIds.AddNumbers, reactive);

            var registry = CreateRegistry();
            var mainExec = main.BuildExecutable(registry);
            var middleware = new List<IGraphMiddleware> { new ReactiveHookMiddleware() };
            var runner = new GraphRunner(middleware);
            var flow = new Flow(CancellationToken.None);
            flow.Blackboard.Set("A", 1);
            flow.Blackboard.Set("B", 1);

            await runner.RunAsync<GameStartEntry>(mainExec, flow, CancellationToken.None);

            Assert.AreEqual(4, LogObjectDefinition.LastLoggedForTests);
        }

        [Test]
        public async Task After_Reactive_Add_Copies_Sum_To_Before_Log()
        {
            LogObjectDefinition.LastLoggedForTests = null;

            var addId = Guid.NewGuid().ToString();
            var logId = Guid.NewGuid().ToString();
            var multId = Guid.NewGuid().ToString();

            var reactive = ScriptableObject.CreateInstance<RuntimeGraph>();
            reactive.ClearSerializedForTests();
            reactive.AppendSerializedNode(multId, GraphFlowDefinitionIds.MultiplyAddInputs);
            reactive.AppendSerializedEntry(typeof(ReactiveChildEntry).AssemblyQualifiedName, multId);

            var main = ScriptableObject.CreateInstance<RuntimeGraph>();
            main.ClearSerializedForTests();
            main.AppendSerializedNode(addId, GraphFlowDefinitionIds.AddNumbers);
            main.AppendSerializedNode(logId, GraphFlowDefinitionIds.LogObject);
            main.AppendSerializedEdge(addId, "Sum", "Message", logId);
            main.AppendSerializedEdge(addId, "Out", "In", logId);
            main.AppendSerializedEntry(typeof(GameStartEntry).AssemblyQualifiedName, addId);
            main.AppendSerializedReactiveHook(MiddlewarePhase.After, GraphFlowDefinitionIds.AddNumbers, reactive);

            var registry = CreateRegistry();
            var mainExec = main.BuildExecutable(registry);
            var runner = new GraphRunner(new List<IGraphMiddleware> { new ReactiveHookMiddleware() });
            var flow = new Flow(CancellationToken.None);
            flow.Blackboard.Set("A", 1);
            flow.Blackboard.Set("B", 1);

            await runner.RunAsync<GameStartEntry>(mainExec, flow, CancellationToken.None);

            Assert.AreEqual(4, LogObjectDefinition.LastLoggedForTests);
        }

        static GraphFlowRegistry CreateRegistry()
        {
            var r = new GraphFlowRegistry();
            r.Register(new AddNumbersDefinition());
            r.Register(new MultiplyAddInputsDefinition());
            r.Register(new LogObjectDefinition());
            return r;
        }
    }
}
