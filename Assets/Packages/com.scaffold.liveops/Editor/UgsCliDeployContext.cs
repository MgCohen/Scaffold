using System;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Scaffold.LiveOps.Editor
{
    /// <summary>Sample: resolves UGS project id and environment name for <c>ugs deploy</c> flags.</summary>
    internal static class UgsCliDeployContext
    {
        private const string notLinkedMessage = "This Unity project is not linked to Unity Gaming Services. Use Edit → Project Settings → Services to sign in and link a project.";
        private const string noEnvMessage = "No active UGS environment name. Set the Editor environment in Edit → Project Settings → Services, or the environment selector in the Services window (signed in). The ugs CLI needs an environment name (e.g. production, development).";

        public static bool TryGetForDeploy(out string projectId, out string environmentName, out string userMessage)
        {
            if (!TryGetProjectId(out projectId, out userMessage))
            {
                environmentName = string.Empty;
                return false;
            }
            if (!TryResolveActiveEnvironmentName(out environmentName, out userMessage))
            {
                return false;
            }
            return true;
        }

        private static bool TryGetProjectId(out string projectId, out string userMessage)
        {
            projectId = CloudProjectSettings.projectId;
            if (string.IsNullOrEmpty(projectId))
            {
                userMessage = notLinkedMessage;
                return false;
            }
            userMessage = null!;
            return true;
        }

        private static bool TryResolveActiveEnvironmentName(out string environmentName, out string userMessage)
        {
            TryReadEnvironmentsApi(out IEnvironmentsApi? envApi, out string? displayName);
            if (TrySetEnvFromName(displayName, out environmentName, out userMessage))
            {
                return true;
            }
            if (TrySetEnvFromName(TryResolveNameFromDeploymentProvider(envApi), out environmentName, out userMessage))
            {
                return true;
            }
            environmentName = null!;
            userMessage = noEnvMessage;
            return false;
        }

        private static bool TrySetEnvFromName(string? name, out string environmentName, out string userMessage)
        {
            if (string.IsNullOrEmpty(name))
            {
                environmentName = null!;
                userMessage = null!;
                return false;
            }
            environmentName = name!;
            userMessage = null!;
            return true;
        }

        private static void TryReadEnvironmentsApi(out IEnvironmentsApi? envApi, out string? displayName)
        {
            envApi = null;
            displayName = null;
            try
            {
                envApi = EnvironmentsApi.Instance;
                displayName = envApi?.ActiveEnvironmentName;
            }
            catch
            {
            }
        }

        private static string? TryResolveNameFromDeploymentProvider(IEnvironmentsApi? envApi)
        {
            string? deployEnvId = null;
            try
            {
                deployEnvId = Deployments.Instance?.EnvironmentProvider?.Current;
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(deployEnvId) || envApi?.Environments == null)
            {
                return null;
            }
            return TryFindNameByGuid(envApi, deployEnvId);
        }

        private static string? TryFindNameByGuid(IEnvironmentsApi envApi, string deployEnvId)
        {
            try
            {
                if (!Guid.TryParse(deployEnvId, out Guid gid))
                {
                    return null;
                }
                return FindNameForEnvId(envApi, gid);
            }
            catch
            {
            }
            return null;
        }

        private static string? FindNameForEnvId(IEnvironmentsApi envApi, Guid gid)
        {
            foreach (EnvironmentInfo info in envApi.Environments)
            {
                if (info.Id == gid)
                {
                    return info.Name;
                }
            }
            return null;
        }
    }
}
