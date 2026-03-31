using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using Scaffold.Maps;
using UnityEngine;

namespace Scaffold.Addressables
{
    public sealed class AddressablesAssetReferenceHandler : IAssetReferenceHandler
    {
        public AddressablesAssetReferenceHandler(IAddressablesAssetClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
        }

        private readonly IAddressablesAssetClient client;
        private readonly Map<Type, string, AddressablesLoadedEntry> loaded = new Map<Type, string, AddressablesLoadedEntry>();
        private readonly object sync = new object();

        public Task<IAssetHandle<T>> AcquireAsync<T>(string key, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            return AcquireAsync<T>(key, PreloadMode.Normal, false, cancellationToken);
        }

        public async Task<IAssetHandle<T>> AcquireAsync<T>(string key, PreloadMode preloadMode, bool isPreload, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Asset key cannot be empty.", nameof(key));
            AddressablesLoadedEntry entry = await AcquireEntryAsync<T>(key, preloadMode, isPreload, cancellationToken);
            if (entry.Asset is not T typedAsset)
            {
                throw new InvalidOperationException($"Loaded asset type mismatch. Requested '{typeof(T).FullName}', actual '{entry.Asset?.GetType().FullName ?? "null"}'.");
            }

            return new AssetHandle<T>(typedAsset, () => ReleaseEntry<T>(key));
        }

        private void ReleaseEntry<T>(string key) where T : UnityEngine.Object
        {
            Type typeKey = typeof(T);
            if (!TryRemoveUnusedEntry(typeKey, key, out AddressablesLoadedEntry loadedEntry))
            {
                return;
            }

            ReleaseLoadedAsset(loadedEntry);
        }

        private bool TryRemoveUnusedEntry(Type typeKey, string key, out AddressablesLoadedEntry loadedEntry)
        {
            lock (sync)
            {
                if (!loaded.TryGetValue(typeKey, key, out loadedEntry))
                {
                    return false;
                }

                DecrementRefUnlessNeverDie(loadedEntry);
                if (ShouldKeepEntry(loadedEntry))
                {
                    return false;
                }

                loaded.Remove(typeKey, key);
                return true;
            }
        }

        private void ReleaseLoadedAsset(AddressablesLoadedEntry loadedEntry)
        {
            client.Release(loadedEntry.Asset);
        }

        private void DecrementRefUnlessNeverDie(AddressablesLoadedEntry loadedEntry)
        {
            if (loadedEntry.RefCount > 0)
            {
                loadedEntry.RefCount--;
            }
        }

        private bool ShouldKeepEntry(AddressablesLoadedEntry loadedEntry)
        {
            return loadedEntry.RefCount > 0 || loadedEntry.Policy == PreloadMode.NeverDie;
        }

        private async Task<AddressablesLoadedEntry> AcquireEntryAsync<T>(string key, PreloadMode preloadMode, bool isPreload, CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            Type typeKey = typeof(T);
            AddressablesLoadedEntry existing = TryGetExistingEntry(typeKey, key, preloadMode, isPreload);
            if (existing != null)
            {
                return existing;
            }

            T asset = await client.LoadAssetAsync<T>(key, cancellationToken);
            return AddOrReuseEntry(typeKey, key, preloadMode, isPreload, asset);
        }

        private AddressablesLoadedEntry TryGetExistingEntry(Type typeKey, string key, PreloadMode preloadMode, bool isPreload)
        {
            lock (sync)
            {
                if (!loaded.TryGetValue(typeKey, key, out AddressablesLoadedEntry existing))
                {
                    return null;
                }

                ApplyPreloadPolicy(existing, preloadMode);
                BumpRefIfNeeded(existing, isPreload);
                return existing;
            }
        }

        private AddressablesLoadedEntry AddOrReuseEntry(Type typeKey, string key, PreloadMode preloadMode, bool isPreload, UnityEngine.Object asset)
        {
            lock (sync)
            {
                if (loaded.TryGetValue(typeKey, key, out AddressablesLoadedEntry existing))
                {
                    ApplyPreloadPolicy(existing, preloadMode);
                    BumpRefIfNeeded(existing, isPreload);
                    return existing;
                }

                AddressablesLoadedEntry created = CreateNewEntry(asset, preloadMode, isPreload);
                loaded.Add(typeKey, key, created);
                return created;
            }
        }

        private void ApplyPreloadPolicy(AddressablesLoadedEntry existing, PreloadMode preloadMode)
        {
            if (preloadMode == PreloadMode.NeverDie)
            {
                existing.Policy = PreloadMode.NeverDie;
            }
        }

        private void BumpRefIfNeeded(AddressablesLoadedEntry existing, bool isPreload)
        {
            if (!isPreload)
            {
                existing.RefCount++;
            }
        }

        private AddressablesLoadedEntry CreateNewEntry(UnityEngine.Object asset, PreloadMode preloadMode, bool isPreload)
        {
            AddressablesLoadedEntry created = new AddressablesLoadedEntry(asset, preloadMode);
            if (!isPreload)
            {
                created.RefCount = 1;
            }

            return created;
        }
    }
}
