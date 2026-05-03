#nullable enable

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class GeneratedMutatorDispatcher : IMutatorDispatcher
    {
        private readonly AddModifierMutator addModifier = new AddModifierMutator();
        private readonly RemoveModifierMutator removeModifier = new RemoveModifierMutator();
        private readonly SetBaseValueMutator setBaseValue = new SetBaseValueMutator();
        private readonly AddEntityVariableMutator addEntityVariable = new AddEntityVariableMutator();
        private readonly RemoveEntityVariableMutator removeEntityVariable = new RemoveEntityVariableMutator();
        private readonly RemoveModifiersBySourceMutator removeModifiersBySource = new RemoveModifiersBySourceMutator();

        public bool TryDispatch<TPayload>(Store store, Reference reference, TPayload payload)
        {
            if (typeof(TPayload) == typeof(AddModifierPayload))
            {
                AddModifierPayload typed = (AddModifierPayload)(object)payload!;
                Reference r = EntityStateReference.From(typed.EntityId);
                store.ExecuteMutator(r, addModifier, typed);
                return true;
            }

            if (typeof(TPayload) == typeof(SetBaseValuePayload))
            {
                SetBaseValuePayload typed = (SetBaseValuePayload)(object)payload!;
                Reference r = EntityStateReference.From(typed.EntityId);
                store.ExecuteMutator(r, setBaseValue, typed);
                return true;
            }

            if (typeof(TPayload) == typeof(AddEntityVariablePayload))
            {
                AddEntityVariablePayload typed = (AddEntityVariablePayload)(object)payload!;
                Reference r = EntityStateReference.From(typed.EntityId);
                store.ExecuteMutator(r, addEntityVariable, typed);
                return true;
            }

            if (typeof(TPayload) == typeof(RemoveEntityVariablePayload))
            {
                RemoveEntityVariablePayload typed = (RemoveEntityVariablePayload)(object)payload!;
                Reference r = EntityStateReference.From(typed.EntityId);
                store.ExecuteMutator(r, removeEntityVariable, typed);
                return true;
            }

            if (typeof(TPayload) == typeof(RemoveModifierPayload))
            {
                RemoveModifierPayload typed = (RemoveModifierPayload)(object)payload!;
                Reference r = typed.GetReference();
                store.ExecuteMutator(r, removeModifier, typed);
                return true;
            }

            if (typeof(TPayload) == typeof(RemoveModifiersBySourcePayload))
            {
                RemoveModifiersBySourcePayload typed = (RemoveModifiersBySourcePayload)(object)payload!;
                Reference r = typed.GetReference();
                store.ExecuteMutator(r, removeModifiersBySource, typed);
                return true;
            }

            return false;
        }
    }
}
