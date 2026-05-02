using Scaffold.EffectGraph;

namespace Scaffold.EffectGraph.Sample.Editor
{
    public abstract class GameEntryPoint { }

    public sealed class SamplePlayEntry : GameEntryPoint
    {
        public bool special;
    }

    public abstract class GameCommand<TResult> where TResult : CommandResult { }

    public abstract class CommandResult { }

    public sealed record StrikeResult : CommandResult
    {
        public int DamageToCore;
    }

    public sealed record Strike : GameCommand<StrikeResult>
    {
        public int Magnitude;
    }
}
