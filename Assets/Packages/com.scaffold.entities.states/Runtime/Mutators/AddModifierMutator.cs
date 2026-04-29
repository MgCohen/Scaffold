#nullable enable
using System.Collections.Immutable;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    public sealed class AddModifierMutator : Mutator<EntityVariableState, AddModifierPayload>
    {
        private readonly EntityBridgeContext context;

        public AddModifierMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        public override EntityVariableState Change(
            EntityVariableState state,
            AddModifierPayload payload,
            IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out var definition))
            {
                return state;
            }

            var bucket = state.ModifierStacks.TryGetValue(payload.Variable, out var existing)
                ? existing
                : ImmutableList<ActiveModifier>.Empty;

            int insertAt = 0;
            while (insertAt < bucket.Count && bucket[insertAt].Modifier.Order <= payload.Modifier.Order)
            {
                insertAt++;
            }

            var nextBucket = bucket.Insert(insertAt, new ActiveModifier(payload.ModifierId, payload.Modifier));
            var nextStacks = state.ModifierStacks.SetItem(payload.Variable, nextBucket);
            var nextEffective = EffectiveValueRecomputer.RecomputeFor(
                state,
                nextStacks,
                payload.Variable,
                definition);

            return state with { ModifierStacks = nextStacks, EffectiveValues = nextEffective };
        }
    }
}
