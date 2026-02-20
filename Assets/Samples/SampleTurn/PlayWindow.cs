using System;
using System.Collections.Generic;

namespace Sample.Turn
{
    /// <summary>
    /// Abstract base for play windows. Concrete subclasses register action types with (validate, execute) pairs; base resolves CanPlay and Execute from the registry.
    /// </summary>
    public abstract class PlayWindow
    {
        private readonly Dictionary<Type, ActionEntry> _handlers = new Dictionary<Type, ActionEntry>();

        protected void Register<TAction>(Func<TAction, bool> validate, Action<TAction, IPlayWindowContext> execute)  where TAction : PlayerAction
        {
            _handlers[typeof(TAction)] = new ActionEntry(
                action => validate((TAction)action),
                (action, context) => execute((TAction)action, context));
        }

        public abstract PlayWindowState CreateInitialState();

        public bool CanPlay(PlayerAction action)
        {
            if (action == null) return false;
            if (!_handlers.TryGetValue(action.GetType(), out var entry)) return false;
            return entry.Validate(action);
        }

        public void Execute(PlayerAction action, IPlayWindowContext context)
        {
            if (action == null) return;
            if (!_handlers.TryGetValue(action.GetType(), out var entry))
                return;
            entry.Execute(action, context);
        }

        private sealed class ActionEntry
        {
            private readonly Func<PlayerAction, bool> _validate;
            private readonly Action<PlayerAction, IPlayWindowContext> _execute;

            public ActionEntry(Func<PlayerAction, bool> validate, Action<PlayerAction, IPlayWindowContext> execute)
            {
                _validate = validate;
                _execute = execute;
            }

            public bool Validate(PlayerAction action)
            {
                return _validate(action);
            }

            public void Execute(PlayerAction action, IPlayWindowContext context)
            {
                _execute(action, context);
            }
        }
    }
}
