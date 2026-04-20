using NUnit.Framework;
using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation.Tests
{
    public sealed class NavigationStackResolverTests
    {
        [Test]
        public void Push_LegacyCloseCurrent_MapsToRemoveCurrent()
        {
            var options = new NavigationOptions { StackPolicy = NavigationStackPolicy.Push };
            NavigationStackResolver.Resolve(options, legacyCloseCurrentParameter: true, out bool closeAll, out bool removeCurrent);
            Assert.That(closeAll, Is.False);
            Assert.That(removeCurrent, Is.True);
        }

        [Test]
        public void Push_LegacyCloseAllViews_MapsToCloseAllBelow()
        {
            var options = new NavigationOptions { StackPolicy = NavigationStackPolicy.Push, CloseAllViews = true };
            NavigationStackResolver.Resolve(options, legacyCloseCurrentParameter: false, out bool closeAll, out bool removeCurrent);
            Assert.That(closeAll, Is.True);
            Assert.That(removeCurrent, Is.False);
        }

        [Test]
        public void ReplaceCurrent_IgnoresLegacyCloseCurrentParameter()
        {
            var options = new NavigationOptions { StackPolicy = NavigationStackPolicy.ReplaceCurrent };
            NavigationStackResolver.Resolve(options, legacyCloseCurrentParameter: false, out bool closeAll, out bool removeCurrent);
            Assert.That(closeAll, Is.False);
            Assert.That(removeCurrent, Is.True);
        }

        [Test]
        public void ClearBelowCurrentAndPush_SetsCloseAll_NotRemove()
        {
            var options = new NavigationOptions { StackPolicy = NavigationStackPolicy.ClearBelowCurrentAndPush };
            NavigationStackResolver.Resolve(options, legacyCloseCurrentParameter: true, out bool closeAll, out bool removeCurrent);
            Assert.That(closeAll, Is.True);
            Assert.That(removeCurrent, Is.False);
        }

        [Test]
        public void ClearAllAndPush_SetsBothFlags()
        {
            var options = new NavigationOptions { StackPolicy = NavigationStackPolicy.ClearAllAndPush };
            NavigationStackResolver.Resolve(options, legacyCloseCurrentParameter: false, out bool closeAll, out bool removeCurrent);
            Assert.That(closeAll, Is.True);
            Assert.That(removeCurrent, Is.True);
        }
    }
}
