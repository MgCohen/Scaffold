#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.GraphFlow.M0;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Mode-2 runner for the card sandbox. Owns the <see cref="CommandPipeline"/> + the
    /// <see cref="IEffectScope"/> handed to commands; graph dispatcher nodes go through
    /// <see cref="Send{TCmd, TResult}"/> instead of executing commands inline.
    /// </summary>
    public sealed class CardEffectRunner : GraphRunner
    {
        public CommandPipeline Pipeline { get; }
        public IEffectScope Scope { get; }

        public CardEffectRunner(IEffectScope scope, CommandPipeline? pipeline = null, CancellationToken token = default)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Pipeline = pipeline ?? new CommandPipeline();
            CancellationToken = token;
        }

        public Task<TResult> Send<TCmd, TResult>(TCmd command) where TCmd : Command<TResult>
            => Pipeline.Send<TCmd, TResult>(command, Scope);
    }
}
