using System;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal sealed class AddressablesNavigationPointStrategy : INavigationPointStrategy
    {
        public AddressablesNavigationPointStrategy(IAddressablesGateway addressables, Transform viewHolder, NavigationAssetHandleBuffer assetHandleBuffer)
        {
            this.addressables = addressables;
            this.viewHolder = viewHolder;
            this.assetHandleBuffer = assetHandleBuffer;
        }

        private readonly IAddressablesGateway addressables;
        private readonly Transform viewHolder;
        private readonly NavigationAssetHandleBuffer assetHandleBuffer;

        public bool TryCreate(ViewConfig config, IViewController controller, NavigationOptions options, out NavigationPoint point)
        {
            if (config == null || config.AssetSource != ViewAssetSource.Addressables)
            {
                point = null;
                return false;
            }

            return CreateAddressablePoint(config, controller, options, out point);
        }

        private bool CreateAddressablePoint(ViewConfig config, IViewController controller, NavigationOptions options, out NavigationPoint point)
        {
            IAssetHandle<GameObject> handle = LoadOrTake(config);
            IAssetHandle<GameObject>[] handleSlot = { handle };
            point = new NavigationPoint(controller, config, false, options, d => DisposeAfterAddressable(config, handleSlot, d));
            _ = MaterializePointAsync(point, () => handleSlot[0], () => handleSlot[0] = null);
            return true;
        }

        private IAssetHandle<GameObject> LoadOrTake(ViewConfig config)
        {
            IAssetHandle<GameObject> handle = assetHandleBuffer.TryTake(config, out IAssetHandle<GameObject> cached) ? cached : null;
            if (handle == null)
            {
                handle = addressables.Load<GameObject>(config.Asset);
            }

            return handle;
        }

        private async Task MaterializePointAsync(NavigationPoint point, Func<IAssetHandle<GameObject>> getHandle, Action clearHandle)
        {
            try
            {
                await CompletePointAsync(point, getHandle);
            }
            catch (Exception exception)
            {
                point?.FailReady(exception);
                IAssetHandle<GameObject> handle = getHandle();
                if (handle != null)
                {
                    handle.Release();
                }

                clearHandle();
            }
        }

        private async Task CompletePointAsync(NavigationPoint point, Func<IAssetHandle<GameObject>> getHandle)
        {
            IAssetHandle<GameObject> handle = getHandle();
            if (point == null || point.Disposed || handle == null)
            {
                return;
            }

            await handle.WhenReady;
            handle = getHandle();
            if (point == null || point.Disposed || handle == null)
            {
                return;
            }

            CompletePointWithInstance(point, handle);
        }

        private void CompletePointWithInstance(NavigationPoint point, IAssetHandle<GameObject> handle)
        {
            GameObject instance = GameObject.Instantiate(handle.Asset, viewHolder);
            IView view = instance.GetComponent<IView>();
            if (view == null)
            {
                DestroyViewObject(instance);
                throw new InvalidOperationException($"Addressable view '{instance.name}' does not implement {nameof(IView)}.");
            }

            instance.SetActive(false);
            point.CompleteReady(view);
        }

        private void DisposeAfterAddressable(ViewConfig config, IAssetHandle<GameObject>[] handleSlot, NavigationPoint disposed)
        {
            ReleaseOrBuffer(config, handleSlot);
            if (disposed == null || disposed.IsSceneView || disposed.View == null)
            {
                return;
            }

            DestroyViewObject(disposed.View.gameObject);
        }

        private void ReleaseOrBuffer(ViewConfig config, IAssetHandle<GameObject>[] handleSlot)
        {
            IAssetHandle<GameObject> handle = handleSlot[0];
            if (handle == null)
            {
                return;
            }

            handleSlot[0] = null;
            if (assetHandleBuffer.Return(config, handle))
            {
                return;
            }

            handle.Release();
        }

        private void DestroyViewObject(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }

            UnityEngine.Object.DestroyImmediate(instance);
        }
    }
}
