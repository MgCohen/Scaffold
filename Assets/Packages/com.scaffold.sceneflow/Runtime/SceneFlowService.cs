using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.SceneFlow.Contracts;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Scaffold.SceneFlow
{
    public sealed class SceneFlowService : ISceneFlowService
    {
        public SceneFlowService(IAddressablesSceneOperations addressablesOperations, ISceneFlowBootstrapShell bootstrapShell = null)
        {
            this.addressablesOperations = addressablesOperations ?? throw new ArgumentNullException(nameof(addressablesOperations));
            this.bootstrapShell = bootstrapShell;
        }

        private readonly IAddressablesSceneOperations addressablesOperations;

        private readonly ISceneFlowBootstrapShell bootstrapShell;

        private readonly Dictionary<Guid, SceneFlowLoadRecord> activeLoads = new Dictionary<Guid, SceneFlowLoadRecord>();

        private int shellManagedLoadCount;

        public async Task<SceneFlowLoadResult> LoadAdditiveAsync(AssetReference sceneReference, SceneFlowLoadOptions options, CancellationToken cancellationToken = default)
        {
            if (sceneReference == null)
            {
                throw new ArgumentNullException(nameof(sceneReference));
            }

            bool manageShell = options.ManageBootstrapShell;
            ReserveShellForAdditiveLoad(manageShell);
            return await RunAdditiveLoadAsync(sceneReference, manageShell, cancellationToken);
        }

        private async Task<SceneFlowLoadResult> RunAdditiveLoadAsync(AssetReference sceneReference, bool manageShell, CancellationToken cancellationToken)
        {
            bool shellStateCommitted = false;
            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = addressablesOperations.LoadSceneAsync(sceneReference, LoadSceneMode.Additive, true, 100);
                (SceneFlowLoadResult loadResult, bool committed) = await CompleteAdditiveLoadAsync(handle, manageShell, cancellationToken);
                shellStateCommitted = committed;
                return loadResult;
            }
            catch
            {
                RollbackShellReservationIfNeeded(manageShell, shellStateCommitted);
                ReleaseHandleIfNeeded(handle);
                throw;
            }
        }

        private void ReserveShellForAdditiveLoad(bool manageShell)
        {
            if (!manageShell)
            {
                return;
            }

            shellManagedLoadCount++;
            if (shellManagedLoadCount == 1)
            {
                bootstrapShell?.SetAdditiveContentActive(true);
            }
        }

        private async Task<(SceneFlowLoadResult Result, bool ShellCommitted)> CompleteAdditiveLoadAsync(AsyncOperationHandle<SceneInstance> handle, bool manageShell, CancellationToken cancellationToken)
        {
            await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"Addressables scene load failed with status {handle.Status}.");
            }

            Guid loadId = Guid.NewGuid();
            string sceneName = handle.Result.Scene.name;
            activeLoads[loadId] = new SceneFlowLoadRecord(handle, manageShell);

            return (new SceneFlowLoadResult(loadId, sceneName, manageShell), true);
        }

        private void RollbackShellReservationIfNeeded(bool manageShell, bool shellStateCommitted)
        {
            if (!manageShell || shellStateCommitted)
            {
                return;
            }

            shellManagedLoadCount--;
            if (shellManagedLoadCount == 0)
            {
                bootstrapShell?.SetAdditiveContentActive(false);
            }
        }

        public async Task UnloadAsync(SceneFlowLoadResult result, CancellationToken cancellationToken = default)
        {
            if (!activeLoads.TryGetValue(result.LoadId, out SceneFlowLoadRecord record))
            {
                throw new InvalidOperationException("Unknown scene flow load id; unload may have already completed.");
            }

            if (!record.SceneLoadHandle.IsValid())
            {
                RemoveActiveLoadRecord(result.LoadId, record);
                return;
            }

            AsyncOperationHandle<SceneInstance> unloadHandle = addressablesOperations.UnloadSceneAsync(record.SceneLoadHandle, true);
            await CompleteUnloadAsync(unloadHandle, result.LoadId, record, cancellationToken);
        }

        private async Task CompleteUnloadAsync(AsyncOperationHandle<SceneInstance> unloadHandle, Guid loadId, SceneFlowLoadRecord record, CancellationToken cancellationToken)
        {
            await unloadHandle.Task;
            cancellationToken.ThrowIfCancellationRequested();

            if (unloadHandle.IsValid() && unloadHandle.Status != AsyncOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"Addressables scene unload failed with status {unloadHandle.Status}.");
            }

            RemoveActiveLoadRecord(loadId, record);
        }

        private void RemoveActiveLoadRecord(Guid loadId, SceneFlowLoadRecord record)
        {
            activeLoads.Remove(loadId);

            if (record.ManageBootstrapShell)
            {
                shellManagedLoadCount--;
                if (shellManagedLoadCount == 0)
                {
                    bootstrapShell?.SetAdditiveContentActive(false);
                }
            }
        }

        private void ReleaseHandleIfNeeded(AsyncOperationHandle<SceneInstance> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }
    }
}
