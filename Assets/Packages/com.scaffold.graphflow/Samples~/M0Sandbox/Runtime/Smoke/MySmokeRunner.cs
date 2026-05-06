using System.Collections.Generic;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    public interface IGraphLogSink
    {
        void Record(string message);
    }

    public sealed class CollectingLogSink : IGraphLogSink
    {
        readonly List<string> _messages = new();
        public IReadOnlyList<string> Messages => _messages;
        public void Record(string message) => _messages.Add(message);
    }

    public sealed class MySmokeRunner : GraphRunner
    {
        public IGraphLogSink LogSink { get; }

        public MySmokeRunner(BakedGraph baked, IGraphLogSink logSink) : base(baked)
        {
            LogSink = logSink;
        }
    }

    public sealed class MySmokeBuilder : GraphBuilder<MySmokeRunner>
    {
        readonly IGraphLogSink _logSink;
        public MySmokeBuilder(IGraphLogSink logSink) { _logSink = logSink; }
        protected override MySmokeRunner CreateRunner(BakedGraph baked) =>
            new(baked, _logSink);
    }
}
