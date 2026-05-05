using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.M0.Smoke
{
    public sealed class MySmokeRunner : GraphRunner
    {
        public string LastLogMessage { get; private set; } = "";

        public void RecordLog(string message) => LastLogMessage = message;

        public MySmokeRunner(BakedGraph baked) : base(baked) { }
    }

    public sealed class MySmokeBuilder : GraphBuilder<MySmokeRunner>
    {
        protected override MySmokeRunner CreateRunner(BakedGraph baked) => new(baked);
    }
}
