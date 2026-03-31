using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scaffold.Addressables.Contracts;
using UnityEngine;

namespace Scaffold.Addressables
{
    internal sealed class ComponentGroupHandle<TComponent> : IAssetGroupHandle<TComponent> where TComponent : Component
    {
        internal ComponentGroupHandle(IAssetGroupHandle<GameObject> gameObjectGroup, IReadOnlyList<TComponent> components)
        {
            if (gameObjectGroup == null)
            {
                throw new ArgumentNullException(nameof(gameObjectGroup));
            }

            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            this.gameObjectGroup = gameObjectGroup;
            this.components = components;
        }

        public bool IsReleased => gameObjectGroup.IsReleased;
        public bool IsReady => gameObjectGroup.IsReady;
        public Task WhenReady => gameObjectGroup.WhenReady;
        public IReadOnlyList<TComponent> Assets => components;

        private readonly IAssetGroupHandle<GameObject> gameObjectGroup;
        private readonly IReadOnlyList<TComponent> components;

        public void Release()
        {
            gameObjectGroup.Release();
        }

        public void Dispose()
        {
            gameObjectGroup.Dispose();
        }
    }
}
