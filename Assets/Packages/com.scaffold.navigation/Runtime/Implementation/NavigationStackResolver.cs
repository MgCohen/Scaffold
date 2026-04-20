using System;
using Scaffold.Navigation.Contracts;

namespace Scaffold.Navigation
{
    internal static class NavigationStackResolver
    {
        public static void Resolve(NavigationOptions options, bool legacyCloseCurrentParameter, out bool closeAllBelowCurrent, out bool removeCurrentFromStack)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            closeAllBelowCurrent = false;
            removeCurrentFromStack = legacyCloseCurrentParameter;

            if (options.StackPolicy != NavigationStackPolicy.Push)
            {
                ApplyExplicitStackPolicy(options.StackPolicy, legacyCloseCurrentParameter, out closeAllBelowCurrent, out removeCurrentFromStack);
                return;
            }

            ApplyLegacyCloseAllViews(options, legacyCloseCurrentParameter, ref closeAllBelowCurrent, ref removeCurrentFromStack);
        }

        private static void ApplyLegacyCloseAllViews(NavigationOptions options, bool legacyCloseCurrentParameter, ref bool closeAllBelowCurrent, ref bool removeCurrentFromStack)
        {
            if (options.CloseAllViews == true)
            {
                closeAllBelowCurrent = true;
            }

            removeCurrentFromStack = legacyCloseCurrentParameter;
        }

        private static void ApplyExplicitStackPolicy(NavigationStackPolicy policy, bool legacyCloseCurrentParameter, out bool closeAllBelowCurrent, out bool removeCurrentFromStack)
        {
            closeAllBelowCurrent = GetCloseAllBelowForExplicitPolicy(policy);
            removeCurrentFromStack = GetRemoveCurrentForExplicitPolicy(policy, legacyCloseCurrentParameter);
        }

        private static bool GetCloseAllBelowForExplicitPolicy(NavigationStackPolicy policy)
        {
            return policy == NavigationStackPolicy.ClearBelowCurrentAndPush || policy == NavigationStackPolicy.ClearAllAndPush;
        }

        private static bool GetRemoveCurrentForExplicitPolicy(NavigationStackPolicy policy, bool legacyCloseCurrentParameter)
        {
            switch (policy)
            {
                case NavigationStackPolicy.ReplaceCurrent:
                    return true;
                case NavigationStackPolicy.ClearBelowCurrentAndPush:
                    return false;
                case NavigationStackPolicy.ClearAllAndPush:
                    return true;
                case NavigationStackPolicy.Push:
                default:
                    return legacyCloseCurrentParameter;
            }
        }
    }
}
