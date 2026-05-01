namespace Scaffold.GraphFlow
{
    public readonly struct GraphRunResult
    {
        public GraphRunResult(bool completedAtReturn, bool stoppedNoNext, object returnValue)
        {
            CompletedAtReturn = completedAtReturn;
            StoppedNoNext = stoppedNoNext;
            ReturnValue = returnValue;
        }

        public bool CompletedAtReturn { get; }
        public bool StoppedNoNext { get; }
        public object ReturnValue { get; }
    }
}
