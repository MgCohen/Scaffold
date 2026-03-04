using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sample.Turn
{
    /// <summary>
    /// Abstract base for play windows. Concrete subclasses register action types with (validate, execute) pairs; base resolves Validate and Execute from the registry.
    /// </summary>
    public abstract class PlayWindow
    {
        private readonly Dictionary<Type, ActionEntry> _handlers = new Dictionary<Type, ActionEntry>();

        protected void Register<TAction>(Func<TAction, Awaitable<bool>> validate, Func<TAction, Awaitable> execute) where TAction : PlayerAction
        {
            _handlers[typeof(TAction)] = new ActionEntry(action => validate((TAction)action), (action) => execute((TAction)action));
        }

        public bool CanPlay(PlayerAction action)
        {
            if (action == null) return false;
            return _handlers.ContainsKey(action.GetType());
        }

        public async Awaitable<bool> Validate(PlayerAction action)
        {
            if (action == null) return false;
            if (!_handlers.TryGetValue(action.GetType(), out var entry)) return false;
            return await Validate(entry, action);
        }

        protected virtual async Awaitable<bool> Validate(ActionEntry entry, PlayerAction action)
        {
            return await entry.Validate(action);
        }

        public async Awaitable Execute(PlayerAction action)
        {
            if (action == null) return;
            if (!_handlers.TryGetValue(action.GetType(), out var entry)) return;
            await Execute(entry, action);
        }

        protected virtual async Awaitable Execute(ActionEntry entry, PlayerAction action)
        {
            await entry.Execute(action);
        }

        protected sealed class ActionEntry
        {
            private readonly Func<PlayerAction, Awaitable<bool>> _validate;
            private readonly Func<PlayerAction, Awaitable> _execute;

            public ActionEntry(Func<PlayerAction, Awaitable<bool>> validate, Func<PlayerAction, Awaitable> execute)
            {
                _validate = validate;
                _execute = execute;
            }

            public Awaitable<bool> Validate(PlayerAction action)
            {
                return _validate(action);
            }

            public Awaitable Execute(PlayerAction action)
            {
                return _execute(action);
            }
        }
    }

    /// <summary>
    /// Generic play window with typed state. Subclasses implement CreateInitialState() returning TState.
    /// </summary>
    public abstract class PlayWindow<TState> : PlayWindow where TState : PlayWindowState
    {
        public abstract TState CreateInitialState();
    }
}
