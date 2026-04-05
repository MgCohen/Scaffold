using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using Scaffold.Scope.Contracts;
using UnityEngine.AddressableAssets;
using UnityAddressables = UnityEngine.AddressableAssets.Addressables;

namespace Scaffold.Addressables
{
    public sealed class AddressablesGateway : IAddressablesGateway, IAsyncInitializable
    {
        public AddressablesGateway(IAddressablesAssetClient client, IAssetReferenceHandler assetReferenceHandler)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.assetReferenceHandler = assetReferenceHandler ?? throw new ArgumentNullException(nameof(assetReferenceHandler));
        }

        private readonly IAddressablesAssetClient client;
        private readonly IAssetReferenceHandler assetReferenceHandler;
        private readonly object initSync = new object();

        private bool initialized;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return InitializeCoreAsync(cancellationToken);
        }

        public async Task<IAssetHandle<T>> LoadAsync<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            GuardRuntimeInvariants();
            cancellationToken.ThrowIfCancellationRequested();
            return await LoadAsync<T>((AssetReference)reference, cancellationToken);
        }

        public async Task<IAssetHandle<T>> LoadAsync<T>(AssetReference reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            GuardRuntimeInvariants();
            cancellationToken.ThrowIfCancellationRequested();
            string key = ResolveReferenceKey(reference);
            return await assetReferenceHandler.AcquireAsync<T>(key, cancellationToken);
        }

        public Task<IAssetGroupHandle<T>> LoadAsync<T>(AssetLabelReference label, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            GuardRuntimeInvariants();
            cancellationToken.ThrowIfCancellationRequested();
            AssetGroupHandle<T> group = new AssetGroupHandle<T>(client.Release);
            _ = CompleteGroupLoadAsync<T>(label, group, cancellationToken);
            return Task.FromResult<IAssetGroupHandle<T>>(group);
        }

        public IAssetHandle<T> Load<T>(AssetReferenceT<T> reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            GuardRuntimeInvariants();
            cancellationToken.ThrowIfCancellationRequested();
            return Load<T>((AssetReference)reference, cancellationToken);
        }

        public IAssetHandle<T> Load<T>(AssetReference reference, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            GuardRuntimeInvariants();
            cancellationToken.ThrowIfCancellationRequested();
            string key = ResolveReferenceKey(reference);
            AssetHandle<T> handle = new AssetHandle<T>();
            _ = CompleteLoadAsync(key, handle, cancellationToken);
            return handle;
        }

        public IAssetGroupHandle<T> Load<T>(AssetLabelReference label, CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            GuardRuntimeInvariants();
            cancellationToken.ThrowIfCancellationRequested();
            AssetGroupHandle<T> group = new AssetGroupHandle<T>(client.Release);
            _ = CompleteGroupLoadAsync<T>(label, group, cancellationToken);
            return group;
        }

        private async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            LogInitializeStart();
            cancellationToken.ThrowIfCancellationRequested();
            if (TrySkipAlreadyInitialized())
            {
                return;
            }

            LogTriggerCatalogSync();
            await EnsureAddressablesRuntimeInitializedAsync(cancellationToken);
            await RunCatalogSyncAsync(cancellationToken);
            MarkInitialized();
        }

        private void LogInitializeStart()
        {
            UnityEngine.Debug.Log($"[AddressablesGateway] InitializeCoreAsync called. Current instance: {this.GetHashCode()}, initialized={initialized}");
        }

        private bool TrySkipAlreadyInitialized()
        {
            lock (initSync)
            {
                if (initialized)
                {
                    UnityEngine.Debug.Log($"[AddressablesGateway] Instance {this.GetHashCode()} is already initialized, skipping sync.");
                    return true;
                }
            }

            return false;
        }

        private void LogTriggerCatalogSync()
        {
            UnityEngine.Debug.Log($"[AddressablesGateway] Instance {this.GetHashCode()} is triggering Catalog Sync...");
        }

        private void MarkInitialized()
        {
            lock (initSync)
            {
                initialized = true;
                UnityEngine.Debug.Log($"[AddressablesGateway] Instance {this.GetHashCode()} set initialized=true.");
            }
        }

        private async Task EnsureAddressablesRuntimeInitializedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = UnityAddressables.InitializeAsync();
            try
            {
                await handle.Task;
            }
            finally
            {
                if (handle.IsValid())
                {
                    UnityAddressables.Release(handle);
                }
            }
        }

        private async Task RunCatalogSyncAsync(CancellationToken cancellationToken)
        {
            try
            {
                UnityEngine.Debug.Log($"[AddressablesGateway] [{this.GetHashCode()}] SyncCatalogAndContentAsync starting...");
                await client.SyncCatalogAndContentAsync(cancellationToken);
                UnityEngine.Debug.Log($"[AddressablesGateway] [{this.GetHashCode()}] SyncCatalogAndContentAsync COMPLETED successfully!");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                UnityEngine.Debug.LogWarning($"Addressables catalog/content sync failed. Continuing startup. {exception.GetType().Name}: {exception.Message}");
            }
        }

        private async Task CompleteGroupLoadAsync<T>(AssetLabelReference label, AssetGroupHandle<T> group, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            try
            {
                GuardLabel(label);
                IReadOnlyList<T> loadedAssets = await client.LoadAssetsByLabelAsync<T>(label, cancellationToken);
                group.CompleteFromAssets(loadedAssets);
            }
            catch (Exception exception)
            {
                group.Fail(exception);
            }
        }

        private async Task CompleteLoadAsync<T>(string key, AssetHandle<T> handle, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            try
            {
                IAssetHandle<T> loaded = await assetReferenceHandler.AcquireAsync<T>(key, cancellationToken);
                handle.Complete(loaded);
            }
            catch (Exception exception)
            {
                handle.Fail(exception);
            }
        }

        private string ResolveReferenceKey(AssetReference reference)
        {
            GuardReference(reference);
            string key = reference.RuntimeKey?.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Asset reference is not valid.", nameof(reference));
            }

            return key;
        }

        private void GuardRuntimeInvariants()
        {
            if (client == null || assetReferenceHandler == null)
            {
                throw new InvalidOperationException("Addressables gateway is not properly initialized.");
            }
        }

        private void GuardLabel(AssetLabelReference label)
        {
            if (label == null || string.IsNullOrWhiteSpace(label.labelString))
            {
                throw new ArgumentException("Label reference cannot be empty.", nameof(label));
            }
        }

        private void GuardReference(AssetReference reference)
        {
            if (reference == null || reference.RuntimeKey == null)
            {
                throw new ArgumentException("Asset reference is not valid.", nameof(reference));
            }
        }
    }
}
