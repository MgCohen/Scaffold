using System.Collections.Generic;
using UnityEngine;

namespace Scaffold.Entities
{
    [CreateAssetMenu(menuName = "Scaffold/Entity/Definition", fileName = "EntityDefinition")]
    public class EntityDefinitionAsset : ScriptableObject, IEntityDefinition, IDefinitionVariableBagProvider
    {
        public IEnumerable<Variable> DefinedVariables => definition.DefinedVariables;

        [SerializeField] private EntityDefinition definition = new EntityDefinition();

        internal IReadOnlyList<VariableEntry> Entries => definition.Entries;

        internal VariableBag Bag => definition.Bag;

        VariableBag IDefinitionVariableBagProvider.Bag => definition.Bag;

        public bool TryGetDefaultValue(Variable key, out VariableValue value)
        {
            return definition.TryGetDefaultValue(key, out value);
        }

        public void AddVariable(Variable key, VariableValue defaultValue)
        {
            definition.AddVariable(key, defaultValue);
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            definition.Bag.EditorApplyVariableAuthoringFromValidation();
#endif
            RebuildLookup();
        }

        internal void RebuildLookup()
        {
            definition.RebuildLookup();
        }

        void IDefinitionVariableBagProvider.RebuildLookup()
        {
            RebuildLookup();
        }
    }
}
