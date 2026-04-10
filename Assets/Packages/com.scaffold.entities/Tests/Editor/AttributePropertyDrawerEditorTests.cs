using NUnit.Framework;
using Scaffold.Entities.Editor;

namespace Scaffold.Entities.Editor.Tests
{
    public sealed class AttributePropertyDrawerEditorTests
    {
        [Test]
        public void AttributePropertyDrawer_Type_IsRegistered()
        {
            Assert.IsNotNull(typeof(AttributePropertyDrawer));
        }

        [Test]
        public void AttributeBagPropertyDrawer_Type_IsRegistered()
        {
            Assert.IsNotNull(typeof(AttributeBagPropertyDrawer));
        }
    }
}
