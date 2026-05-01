using UnityEngine;
using Scaffold.Types;
using Scaffold.Navigation.Contracts;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
namespace Scaffold.Navigation.Utility
{
    public static class NavigationExtensions
    {
        public static TViewController Open<TViewController>(this INavigation navigation, TViewController controller = default, bool closeOpenedWindow = false) where TViewController : IViewController, new()
        {
            controller ??= new TViewController();
            navigation.Open(controller, closeOpenedWindow);
            return controller;
        }

        public static void OpenReplace<TViewController>(this INavigation navigation, TViewController controller) where TViewController : IViewController
        {
            navigation.Open(controller, new NavigationOptions { StackPolicy = NavigationStackPolicy.ReplaceCurrent });
        }

        public static void OpenClearBelowAndPush<TViewController>(this INavigation navigation, TViewController controller) where TViewController : IViewController
        {
            navigation.Open(controller, new NavigationOptions { StackPolicy = NavigationStackPolicy.ClearBelowCurrentAndPush });
        }

        public static void OpenClearAllAndPush<TViewController>(this INavigation navigation, TViewController controller) where TViewController : IViewController
        {
            navigation.Open(controller, new NavigationOptions { StackPolicy = NavigationStackPolicy.ClearAllAndPush });
        }
    }
}




