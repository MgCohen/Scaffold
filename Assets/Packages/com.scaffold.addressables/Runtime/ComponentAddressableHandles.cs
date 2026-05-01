using System;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine;

namespace Scaffold.Addressables
{
    internal sealed class ComponentAssetHandle<TComponent> : IAssetHandle<TComponent> where TComponent : Component
    {
        public ComponentAssetHandle(IAssetHandle<GameObject> gameObjectHandle, TComponent component)
        {
            if (gameObjectHandle == null)
            {
                throw new ArgumentNullException(nameof(gameObjectHandle));
            }

            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }

            this.gameObjectHandle = gameObjectHandle;
            this.component = component;
        }

        public Type AssetType => typeof(TComponent);
        public UnityEngine.Object UntypedAsset => component;
        public TComponent Asset => component;
        public bool IsReleased => gameObjectHandle.IsReleased;
        public AssetHandleState State => gameObjectHandle.State;
        public bool IsReady => gameObjectHandle.IsReady;
        public Task WhenReady => gameObjectHandle.WhenReady;

        private readonly IAssetHandle<GameObject> gameObjectHandle;
        private readonly TComponent component;

        public void Release()
        {
            gameObjectHandle.Release();
        }
    }
}
