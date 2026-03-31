using UnityEngine;
using Scaffold.Navigation.Contracts;
namespace Scaffold.Navigation.Samples
{
    public class NavigationUseCases
    {
        public void UseCaseNavigationOptions()
        {
            NavigationOptions options = new NavigationOptions();
            options.CloseAllViews = true;
            options.RenderOverride = RenderMode.ScreenSpaceCamera;
            Debug.Log($"CloseAll: {options.CloseAllViews}, RenderOverride: {options.RenderOverride}");
        }

        public void UseCaseOpenViewWithOptions()
        {
            INavigation navigation = BuildGetSampleNavigation();
            NavigationOptions options = new NavigationOptions();
            options.CloseAllViews = false;
            SampleViewController controller = new SampleViewController();
            navigation.Open(controller, closeCurrent: false, options: options);
        }

        private static INavigation BuildGetSampleNavigation()
        {
            return new NullNavigation();
        }

        private class SampleViewController : IViewController
        {
            public void Bind(INavigation navigation) { }
            public void Close() { }
        }

        private class NullNavigation : INavigation
        {
            public IViewController CurrentController => null;
            public void Open<TController>(TController controller, bool closeCurrent = false, NavigationOptions options = null) where TController : IViewController { }
            public void Close<TViewController>(TViewController controller) where TViewController : IViewController { }
            public IViewController Return() { return null; }
        }
    }
}




