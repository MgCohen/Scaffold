using Scaffold.States;

namespace Scaffold.Entities.States
{
    public static class EntityBridgeContext
    {
        public static void RegisterMutators(Store store)
        {
            if (store == null)
            {
                throw new System.ArgumentNullException(nameof(store));
            }

            store.RegisterMutator(new AddModifierMutator());
            store.RegisterMutator(new RemoveModifierMutator());
            store.RegisterMutator(new SetBaseValueMutator());
            store.RegisterMutator(new AddEntityVariableMutator());
        }
    }
}
