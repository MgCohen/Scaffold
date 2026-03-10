using System.Collections.Generic;

namespace Scaffold.Entities
{
    public sealed class EntityRegistry : IEntityRegistry
    {
        private readonly Dictionary<string, EntityDefinition> definitions = new Dictionary<string, EntityDefinition>();
        private readonly Dictionary<string, IEntityInstance> instances = new Dictionary<string, IEntityInstance>();
        private readonly HashSet<string> registeredIds = new HashSet<string>();

        public bool RegisterDefinition(EntityDefinition definition)
        {
            if (!TryGetDefinitionId(definition, out string id)) { return false; }
            if (!TryRegisterId(id)) { return false; }
            definitions[id] = definition;
            return true;
        }

        public bool RegisterInstance(IEntityInstance instance)
        {
            if (!TryGetInstanceId(instance, out string instanceId)) { return false; }
            if (!HasRegisteredDefinition(instance)) { return false; }
            if (!TryRegisterId(instanceId)) { return false; }
            instances[instanceId] = instance;
            return true;
        }

        public bool TryGetDefinition(string id, out EntityDefinition definition)
        {
            return definitions.TryGetValue(id, out definition);
        }

        public bool TryGetInstance(string id, out IEntityInstance instance)
        {
            return instances.TryGetValue(id, out instance);
        }

        public bool UnregisterDefinition(string id)
        {
            if (IsDefinitionReferenced(id)) { return false; }
            bool wasRemoved = definitions.Remove(id);
            if (!wasRemoved) { return false; }
            registeredIds.Remove(id);
            return true;
        }

        public bool UnregisterInstance(string id)
        {
            bool wasRemoved = instances.Remove(id);
            if (!wasRemoved) { return false; }
            registeredIds.Remove(id);
            return true;
        }

        public void Clear()
        {
            definitions.Clear();
            instances.Clear();
            registeredIds.Clear();
        }

        private bool TryGetDefinitionId(EntityDefinition definition, out string id)
        {
            id = null;
            if (definition == null) { return false; }
            id = definition.Id;
            return !string.IsNullOrEmpty(id);
        }

        private bool TryGetInstanceId(IEntityInstance instance, out string id)
        {
            id = null;
            if (instance == null) { return false; }
            id = instance.Id;
            return !string.IsNullOrEmpty(id);
        }

        private bool HasRegisteredDefinition(IEntityInstance instance)
        {
            EntityDefinition definition = instance.Definition;
            if (definition == null) { return false; }
            string definitionId = definition.Id;
            if (string.IsNullOrEmpty(definitionId)) { return false; }
            return definitions.ContainsKey(definitionId);
        }

        private bool TryRegisterId(string id)
        {
            return registeredIds.Add(id);
        }

        private bool IsDefinitionReferenced(string definitionId)
        {
            foreach (IEntityInstance instance in instances.Values) { EntityDefinition definition = instance.Definition; if (definition != null && definition.Id == definitionId) { return true; } }
            return false;
        }
    }
}
