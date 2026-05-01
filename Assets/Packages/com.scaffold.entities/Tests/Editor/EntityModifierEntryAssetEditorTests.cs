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
                soVar.FindProperty("payloadTypeId").stringValue = "float";
                soVar.ApplyModifiedPropertiesWithoutUndo();
            }

            var asset = ScriptableObject.CreateInstance<EntityModifierEntryAsset>();
            using (var soAsset = new SerializedObject(asset))
            {
                SerializedProperty entry = soAsset.FindProperty("entry");
                Assert.IsNotNull(entry);
                entry.FindPropertyRelative("variableLegacy").objectReferenceValue = healthVariable;

                SerializedProperty modifier = entry.FindPropertyRelative("modifier");
                Assert.IsNotNull(modifier);
                modifier.managedReferenceValue = new FloatAddModifier(25f);
                soAsset.ApplyModifiedPropertiesWithoutUndo();
            }

            var runtime = (EntityModifierEntry)asset;
            Assert.IsNotNull(runtime);
            Assert.AreEqual((Variable)healthVariable, runtime.Key);
            Assert.IsInstanceOf<FloatAddModifier>(runtime.Modifier);
            Assert.AreEqual(25f, ((FloatAddModifier)runtime.Modifier).Apply(0f), 0.0001f);
        }
    }
}
