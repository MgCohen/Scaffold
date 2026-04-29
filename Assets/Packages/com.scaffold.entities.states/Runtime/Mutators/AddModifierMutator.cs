#nullable enable
using System.Collections.Generic;

using Scaffold.Entities;
using Scaffold.States;

namespace Scaffold.Entities.States
{
    internal sealed class AddModifierMutator : Mutator<EntityVariableState, AddModifierPayload>
    {
        public AddModifierMutator(EntityBridgeContext context)
        {
            this.context = context;
        }

        private readonly EntityBridgeContext context;

        public override EntityVariableState Change(EntityVariableState state, AddModifierPayload payload, IStateScope scope)
        {
            if (!context.TryGetDefinition(payload.EntityId, out IEntityDefinition? definition)) return state;

            var nextStacks = EntityVariableState.CreateMutableStacks(state.ModifierStacks);
            var nextBucket = CreateBucketCopy(nextStacks, payload.Variable);
            int insertAt = FindInsertIndex(nextBucket, payload.Modifier.Order);
            nextBucket.Insert(insertAt, new ActiveModifier(payload.ModifierId, payload.Modifier));
            nextStacks[payload.Variable] = nextBucket;

            var nextEffective = EffectiveValueRecomputer.RecomputeFor(state.BaseValues, nextStacks, state.EffectiveValues, payload.Variable, definition);
            return state with { ModifierStacks = nextStacks, EffectiveValues = nextEffective };
        }

        private int FindInsertIndex(List<ActiveModifier> bucket, int order)
        {
            int insertAt = 0;
            while (insertAt < bucket.Count && bucket[insertAt].Modifier.Order <= order)
            {
                insertAt++;
            }

            return insertAt;
        }

        private static List<ActiveModifier> CreateBucketCopy(Dictionary<Variable, IReadOnlyList<ActiveModifier>> stacks, Variable variable)
        {
            return stacks.TryGetValue(variable, out IReadOnlyList<ActiveModifier>? existing) && existing != null ? new List<ActiveModifier>(existing) : new List<ActiveModifier>();
        }
    }
}
