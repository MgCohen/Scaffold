using NUnit.Framework;
using Scaffold.Entities.Editor;

namespace Scaffold.Entities.Editor.Tests
{
    public sealed class VariablePropertyDrawerEditorTests
    {
        [Test]
        public void VariablePropertyDrawer_Type_IsRegistered()
        {
            Assert.IsNotNull(typeof(VariablePropertyDrawer));
        }

        [Test]
        public void VariableBagPropertyDrawer_Type_IsRegistered()
        {
            Assert.IsNotNull(typeof(VariableBagPropertyDrawer));
        }

        [Test]
        public void EntityModifierEntryDrawer_Type_IsRegistered()
        {
            Assert.IsNotNull(typeof(EntityModifierEntryDrawer));
        }
    }
}
