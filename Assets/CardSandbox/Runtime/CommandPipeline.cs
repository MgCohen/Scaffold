#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Per-runner pipeline that dispatches commands through registered listeners and finally to
    /// <see cref="Command{TResult}.Execute"/>. Listeners are stored per closed cmd/result pair.
    ///
    /// <para>Reflection lives only in <see cref="Send"/> to look up the listener list for the
    /// concrete <typeparamref name="TCmd"/>; the runtime path is allocation-light (one boxed list +
    /// one closure per Send).</para>
    /// </summary>
    public sealed class CommandPipeline
    {
        readonly Dictionary<Type, object> _listenersByCmd = new();

        public void Register<TCmd, TResult>(ICommandListener<TCmd, TResult> listener)
            where TCmd : Command<TResult>
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));
            var key = typeof(TCmd);
            if (!_listenersByCmd.TryGetValue(key, out var box))
            {
                box = new List<ICommandListener<TCmd, TResult>>();
                _listenersByCmd[key] = box;
            }

            ((List<ICommandListener<TCmd, TResult>>)box).Add(listener);
        }

        public Task<TResult> Send<TCmd, TResult>(TCmd command, IEffectScope scope)
            where TCmd : Command<TResult>
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            CommandNext<TResult> next = () => command.Execute(scope);

            if (_listenersByCmd.TryGetValue(typeof(TCmd), out var box))
            {
                var listeners = (List<ICommandListener<TCmd, TResult>>)box;
                // Build the chain inside-out so listeners[0] runs outermost.
                for (var i = listeners.Count - 1; i >= 0; i--)
                {
                    var listener = listeners[i];
                    var inner = next;
                    next = () => listener.Intercept(command, scope, inner);
                }
            }

            return next();
        }
    }
}
