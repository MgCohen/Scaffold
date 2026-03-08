using NUnit.Framework;
using Scaffold.MVVM.Binding;

namespace Scaffold.MVVM.Tests
{
    public class MVVMTests
    {
        [Test]
        public void BindedProperty_Update_WithMatchingType_InvokesSetter()
        {
            string received = null;
            BindSet<string, string> bindSet = new BindSet<string, string>();
            BindedProperty<string, string> prop = new BindedProperty<string, string>(bindSet, v => received = v);
            prop.Update("hello");
            Assert.AreEqual("hello", received);
        }

        [Test]
        public void BindedProperty_Update_WithIntToString_AutoConvertsViaToString()
        {
            string received = null;
            BindSet<int, string> bindSet = new BindSet<int, string>();
            BindedProperty<int, string> prop = new BindedProperty<int, string>(bindSet, v => received = v);
            prop.Update(42);
            Assert.AreEqual("42", received);
        }

        [Test]
        public void BindingPath_Create_WithDottedPath_ReturnsExpectedTopSegment()
        {
            BindingPath path = BindingPath.Create("viewModel.Player.Name");
            Assert.AreEqual("viewModel.Player.Name", path.Path);
        }
    }
}
