using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine;

namespace Scaffold.Addressables
{
    internal sealed class AssetGroupHandle<T> : IAssetGroupHandle<T> where T : UnityEngine.Object
    {
        internal AssetGroupHandle(Action<UnityEngine.Object> releaseAsset)
        {
            this.releaseAsset = releaseAsset ?? throw new ArgumentNullException(nameof(releaseAsset));
        }

        public bool IsReleased => releasedFlag != 0;
        public bool IsReady => ready;
        public Task WhenReady => completion.Task;
        public IReadOnlyList<T> Assets => assets;

        private readonly object sync = new object();
        private readonly List<T> assets = new List<T>();
        private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        private readonly Action<UnityEngine.Object> releaseAsset;

        private bool ready;
        private int releasedFlag;

        internal void CompleteFromAssets(IReadOnlyList<T> loadedAssets)
        {
            if (loadedAssets == null)
            {
                throw new ArgumentNullException(nameof(loadedAssets));
            }

            if (TryCompleteReleased())
            {
                return;
            }

            CopyNonNullAssets(loadedAssets);
            completion.TrySetResult(true);
        }

        internal void Fail(Exception exception)
        {
            if (exception == null)
            {
                InvalidOperationException fallback = new InvalidOperationException("Group load failed.");
                completion.TrySetException(fallback);
                return;
            }

            completion.TrySetException(exception);
        }

        public void Dispose()
        {
            Release();
        }

        public void Release()
        {
            if (Interlocked.Exchange(ref releasedFlag, 1) != 0)
            {
                return;
            }

            ReleaseAllAssets();
            completion.TrySetResult(true);
        }

        private bool TryCompleteReleased()
        {
            lock (sync)
            {
                if (IsReleased)
                {
                    completion.TrySetResult(true);
                    return true;
                }
            }

            return false;
        }

        private void CopyNonNullAssets(IReadOnlyList<T> loadedAssets)
        {
            lock (sync)
            {
                assets.Clear();
                for (int i = 0; i < loadedAssets.Count; i++)
                {
                    T typedAsset = loadedAssets[i];
                    if (typedAsset != null)
                    {
                        assets.Add(typedAsset);
                    }
                }

                ready = true;
            }
        }

        private void ReleaseAllAssets()
        {
            List<T> snapshot = new List<T>();
            lock (sync)
            {
                for (int i = 0; i < assets.Count; i++)
                {
                    snapshot.Add(assets[i]);
                }
                assets.Clear();
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                releaseAsset(snapshot[i]);
            }
        }
    }
}
