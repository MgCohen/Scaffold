using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Definition", fileName = "EntityDefinition")]
    public class EntityDefinitionAsset : ScriptableObject, IEntityDefinition
    {
        internal IReadOnlyList<VariableEntry> Entries => bag.Entries;

        internal VariableBag Bag => bag;

        [SerializeField] private VariableBag bag = new VariableBag();

        public IEnumerable<Variable> DefinedVariables => bag.LocalKeys;

        public bool TryGetDefaultValue(Variable key, out VariableValue value)
        {
            return bag.TryGetBase(key, out value);
        }

        public void AddVariable(Variable key, VariableValue defaultValue)
        {
            bag.AddSerializedEntry(VariableEntry.Create(key, defaultValue));
            RebuildLookup();
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            bag.EditorApplyVariableAuthoringFromValidation();
#endif
            RebuildLookup();
        }

        internal void RebuildLookup()
        {
            bag.RebuildCache();
        }
    }
}
