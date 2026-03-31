using System;
using System.Threading;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine;

namespace Scaffold.Addressables
{
    internal sealed class AssetHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
    {
        public AssetHandle(T asset, Action onRelease)
        {
            GuardConstructor(asset, onRelease);
            this.asset = asset;
            this.onRelease = onRelease;
            state = AssetHandleState.Ready;
            completion.TrySetResult(true);
        }

        public AssetHandle()
        {
            state = AssetHandleState.Loading;
        }

        public Type AssetType => typeof(T);
        public UnityEngine.Object UntypedAsset => IsReady ? asset : null;
        public T Asset
        {
            get
            {
                if (!IsReady)
{
    throw new InvalidOperationException("Asset handle is not ready.");
}
                return asset;
            }
        }
        public bool IsReleased => releasedFlag != 0;
        public AssetHandleState State => state;
        public bool IsReady => state == AssetHandleState.Ready;
        public Task WhenReady => completion.Task;

        private readonly TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
        private Action onRelease;
        private IAssetHandle<T> inner;
        private T asset;
        private AssetHandleState state;
        private int releasedFlag;

        internal void Complete(IAssetHandle<T> loadedHandle)
        {
            if (loadedHandle == null)
{
    throw new ArgumentNullException(nameof(loadedHandle));
}
            if (state != AssetHandleState.Loading) return;
            ApplyCompletedHandle(loadedHandle);
        }

        private void ApplyCompletedHandle(IAssetHandle<T> loadedHandle)
        {
            inner = loadedHandle;
            asset = loadedHandle.Asset;
            state = IsReleased ? AssetHandleState.Released : AssetHandleState.Ready;
            completion.TrySetResult(true);
            if (IsReleased) loadedHandle.Release();
        }

        internal void Fail(Exception exception)
        {
            if (exception == null)
{
    throw new ArgumentNullException(nameof(exception));
}
            if (state != AssetHandleState.Loading)
{
    return;
}
            state = IsReleased ? AssetHandleState.Released : AssetHandleState.Faulted;
            completion.TrySetException(exception);
        }

        public void Release()
        {
            if (Interlocked.Exchange(ref releasedFlag, 1) != 0)
{
    return;
}
            if (state == AssetHandleState.Loading)
{
    return;
}
            ReleaseReadyHandle();
            state = AssetHandleState.Released;
        }

        private void ReleaseReadyHandle()
        {
            if (state != AssetHandleState.Ready)
{
    return;
}
            if (inner != null)
{
    inner.Release(); return;
}
            onRelease?.Invoke();
        }

        private void GuardConstructor(T asset, Action onRelease)
        {
            if (asset == null)
{
    throw new ArgumentNullException(nameof(asset));
}
            if (onRelease == null)
{
    throw new ArgumentNullException(nameof(onRelease));
}
        }
    }
}


