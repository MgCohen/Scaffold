using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Scaffold.Addressables
{
    public sealed class AddressablesAssetClient : IAddressablesAssetClient
    {
        public async Task SyncCatalogAndContentAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            var checkHandle = UnityEngine.AddressableAssets.Addressables.CheckForCatalogUpdates(false);
            IList<string> catalogs = await checkHandle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(checkHandle);
            cancellationToken.ThrowIfCancellationRequested();
            if (catalogs == null || catalogs.Count == 0) return;
            var updateHandle = UnityEngine.AddressableAssets.Addressables.UpdateCatalogs(catalogs, false);
            await updateHandle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(updateHandle);
            cancellationToken.ThrowIfCancellationRequested();
            await DownloadCatalogDependenciesAsync(catalogs, cancellationToken);
        }

        public async Task<T> LoadAssetAsync<T>(string key, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Asset key cannot be empty.", nameof(key));
            var locationsHandle = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(key, typeof(T));
            IList<IResourceLocation> locations = await locationsHandle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(locationsHandle);
            cancellationToken.ThrowIfCancellationRequested();
            if (locations == null || locations.Count == 0)
            {
                throw new InvalidOperationException($"Addressables key '{key}' with type '{typeof(T).FullName}' was not found in resource locations.");
            }

            var assetHandle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(key);
            T asset = await assetHandle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            EnsureAssetWasLoaded(asset, key);
            return asset;
        }

        public async Task<IReadOnlyList<T>> LoadAssetsByLabelAsync<T>(AssetLabelReference label, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            if (label == null || string.IsNullOrWhiteSpace(label.labelString)) throw new ArgumentException("Label reference cannot be empty.", nameof(label));

            var locationsHandle = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(label, typeof(T));
            IList<IResourceLocation> locations = await locationsHandle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(locationsHandle);
            cancellationToken.ThrowIfCancellationRequested();
            if (locations == null || locations.Count == 0)
            {
                return Array.Empty<T>();
            }

            var handle = UnityEngine.AddressableAssets.Addressables.LoadAssetsAsync<T>(locations, null, false);
            IList<T> loadedAssets = await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return ToNonNullList(loadedAssets);
        }

        public async Task<IReadOnlyList<string>> ResolveLabelAsync<T>(AssetLabelReference label, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            if (label == null || string.IsNullOrWhiteSpace(label.labelString)) throw new ArgumentException("Label reference cannot be empty.", nameof(label));
            var locationsHandle = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(label, typeof(T));
            IList<IResourceLocation> locations = await locationsHandle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(locationsHandle);
            cancellationToken.ThrowIfCancellationRequested();
            return ToDistinctKeys(locations);
        }

        public void Release(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            UnityEngine.AddressableAssets.Addressables.Release(asset);
        }

        private async Task DownloadCatalogDependenciesAsync(IList<string> catalogs, CancellationToken cancellationToken)
        {
            foreach (string catalog in catalogs)
            {
                await DownloadCatalogIfNeededAsync(catalog, cancellationToken);
            }
        }

        private async Task DownloadCatalogIfNeededAsync(string catalog, CancellationToken cancellationToken)
        {
            var sizeHandle = UnityEngine.AddressableAssets.Addressables.GetDownloadSizeAsync(catalog);
            long size = await sizeHandle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(sizeHandle);
            cancellationToken.ThrowIfCancellationRequested();
            if (size <= 0) return;
            await DownloadDependenciesAsync(catalog, cancellationToken);
        }

        private async Task DownloadDependenciesAsync(string catalog, CancellationToken cancellationToken)
        {
            var handle = UnityEngine.AddressableAssets.Addressables.DownloadDependenciesAsync(catalog, UnityEngine.AddressableAssets.Addressables.MergeMode.Union, false);
            await handle.Task;
            UnityEngine.AddressableAssets.Addressables.Release(handle);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private IReadOnlyList<T> ToNonNullList<T>(IList<T> loadedAssets) where T : UnityEngine.Object
        {
            if (loadedAssets == null)
            {
                return Array.Empty<T>();
            }

            List<T> list = new List<T>(loadedAssets.Count);
            for (int i = 0; i < loadedAssets.Count; i++)
            {
                T asset = loadedAssets[i];
                if (asset != null)
                {
                    list.Add(asset);
                }
            }

            return list;
        }

        private void EnsureAssetWasLoaded<T>(T asset, string key) where T : UnityEngine.Object
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"Addressables returned null for key '{key}' and type '{typeof(T).FullName}'.");
            }
        }

        private IReadOnlyList<string> ToDistinctKeys(IList<IResourceLocation> locations)
        {
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            List<string> keys = new List<string>();
            foreach (IResourceLocation location in locations)
            {
                AddDistinctLocationKey(location, unique, keys);
            }

            return keys;
        }

        private void AddDistinctLocationKey(IResourceLocation location, ISet<string> unique, ICollection<string> keys)
        {
            if (!ShouldIncludeLocation(location, unique))
            {
                return;
            }

            keys.Add(location.PrimaryKey);
        }

        private bool ShouldIncludeLocation(IResourceLocation location, ISet<string> uniqueKeys)
        {
            if (location == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(location.PrimaryKey))
            {
                return false;
            }

            return uniqueKeys.Add(location.PrimaryKey);
        }
    }
}
