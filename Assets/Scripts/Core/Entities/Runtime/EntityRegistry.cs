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
            if (definition == null) { return false; }
            string id = definition.Id;
            if (string.IsNullOrEmpty(id)) { return false; }
            if (!TryRegisterId(id)) { return false; }
            definitions[id] = definition;
            return true;
        }

        public bool RegisterInstance(IEntityInstance instance)
        {
            if (instance == null) { return false; }
            string instanceId = instance.Id;
            if (string.IsNullOrEmpty(instanceId)) { return false; }
            string definitionId = instance.DefinitionId;
            if (!definitions.ContainsKey(definitionId)) { return false; }
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

        private bool TryRegisterId(string id)
        {
            return registeredIds.Add(id);
        }

        private bool IsDefinitionReferenced(string definitionId)
        {
            foreach (KeyValuePair<string, IEntityInstance> pair in instances)
            {
                IEntityInstance instance = pair.Value;
                bool isReference = instance.DefinitionId == definitionId;
                if (isReference) { return true; }
            }
            return false;
        }
    }
}
