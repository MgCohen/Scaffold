#nullable enable
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class GeneratedMutatorDispatcher : IMutatorDispatcher
    {
        private readonly AddModifierMutator addModifier = new();
        private readonly RemoveModifierMutator removeModifier = new();
        private readonly SetBaseValueMutator setBaseValue = new();
        private readonly AddEntityVariableMutator addEntityVariable = new();
        private readonly RemoveEntityVariableMutator removeEntityVariable = new();
        private readonly RemoveModifiersBySourceMutator removeModifiersBySource = new();

        public bool TryDispatch<TPayload>(Store store, Reference reference, TPayload payload)
        {
            if (payload is AddModifierPayload addMod)
            {
                store.ExecuteMutator(addMod.EntityRef, addModifier, addMod);
                return true;
            }

            if (payload is SetBaseValuePayload setBase)
            {
                store.ExecuteMutator(setBase.EntityRef, setBaseValue, setBase);
                return true;
            }

            if (payload is AddEntityVariablePayload addVar)
            {
                store.ExecuteMutator(addVar.EntityRef, addEntityVariable, addVar);
                return true;
            }

            if (payload is RemoveEntityVariablePayload removeVar)
            {
                store.ExecuteMutator(removeVar.EntityRef, removeEntityVariable, removeVar);
                return true;
            }

            if (payload is RemoveModifierPayload removeMod)
            {
                store.ExecuteMutator(removeMod.EntityRef, removeModifier, removeMod);
                return true;
            }

            if (payload is RemoveModifiersBySourcePayload removeBySource)
            {
                store.ExecuteMutator(removeBySource.EntityRef, removeModifiersBySource, removeBySource);
                return true;
            }

            return false;
        }
    }
}
