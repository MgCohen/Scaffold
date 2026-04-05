using System;

namespace Scaffold.States
{
    public static class StoreBuilderMethods
    {
        public static StoreBuilder BuildSlice<TState>(this StoreBuilder storeBuilder, IReference reference, TState state) where TState: State
        {
            storeBuilder.AddState(reference, state);
            return storeBuilder;
        }

        public static StoreBuilder BuildSlice<TState>(this StoreBuilder storeBuilder, TState state) where TState : State
        {
            storeBuilder.AddState(state);
            return storeBuilder;
        }

        public static StoreBuilder WithBuilder<TRef, TState>(this StoreBuilder storeBuilder, StateBuilder<TRef, TState> builder, params TRef[] refs) where TRef : IReference where TState: State
        {
            foreach(var reference in refs)
            {
                var state = builder.Build(reference);
                storeBuilder.AddState(reference, state);
            }
            return storeBuilder;
        }

        public static StoreBuilder WithBuilder<TRef, TState>(this StoreBuilder storeBuilder, Func<TRef, TState> factoryMethod, params TRef[] refs) where TRef : IReference where TState: State
        {
            GenericStateBuilder<TRef, TState> builder = new GenericStateBuilder<TRef, TState>(factoryMethod);
            return WithBuilder(storeBuilder, builder, refs);
        }
    }
}
