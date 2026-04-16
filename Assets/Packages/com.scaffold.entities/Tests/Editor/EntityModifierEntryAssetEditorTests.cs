using NUnit.Framework;
using Scaffold.Entities;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Entities.Editor.Tests
{
    public sealed class EntityModifierEntryAssetEditorTests
    {
        [Test]
        public void EntityModifierEntryAsset_ExplicitCast_CopiesVariableAndModifierPayload()
        {
            var healthVariable = ScriptableObject.CreateInstance<VariableSO>();
            using (var soVar = new SerializedObject(healthVariable))
            {
                soVar.FindProperty("valueType").enumValueIndex = (int)VariableValueType.Float;
                soVar.ApplyModifiedPropertiesWithoutUndo();
            }

            var asset = ScriptableObject.CreateInstance<EntityModifierEntryAsset>();
            using (var soAsset = new SerializedObject(asset))
            {
                SerializedProperty entry = soAsset.FindProperty("entry");
                Assert.IsNotNull(entry);
                entry.FindPropertyRelative("variable").objectReferenceValue = healthVariable;

                SerializedProperty modifierValue = entry.FindPropertyRelative("modifierValue");
                Assert.IsNotNull(modifierValue);
                modifierValue.managedReferenceValue = new FloatVariableValue { Value = 25f };
                soAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            var runtime = (EntityModifierEntry)asset;
            Assert.IsNotNull(runtime);
            Assert.AreSame(healthVariable, runtime.Variable);
            Assert.IsInstanceOf<FloatVariableValue>(runtime.ModifierValue);
            Assert.AreEqual(25f, ((FloatVariableValue)runtime.ModifierValue).Value, 0.0001f);
        }
    }
}
