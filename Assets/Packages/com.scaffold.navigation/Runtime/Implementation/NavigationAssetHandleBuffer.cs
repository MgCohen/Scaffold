using System;
using System.Collections.Generic;
using Scaffold.Addressables.Contracts;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal sealed class NavigationAssetHandleBuffer
    {
        public NavigationAssetHandleBuffer(int maxPerView = 2)
        {
            this.maxPerView = Math.Max(1, maxPerView);
        }

        private readonly int maxPerView;
        private readonly Dictionary<ViewConfig, Stack<IAssetHandle<GameObject>>> byConfig = new Dictionary<ViewConfig, Stack<IAssetHandle<GameObject>>>();

        public bool TryTake(ViewConfig config, out IAssetHandle<GameObject> handle)
        {
            handle = null;
            if (config == null || !byConfig.TryGetValue(config, out Stack<IAssetHandle<GameObject>> pool))
            {
                return false;
            }

            return TryPopReusableHandle(pool, out handle);
        }

        private bool TryPopReusableHandle(Stack<IAssetHandle<GameObject>> pool, out IAssetHandle<GameObject> handle)
        {
            while (pool.Count > 0)
            {
                IAssetHandle<GameObject> cached = pool.Pop();
                if (!IsReusable(cached))
                {
                    continue;
                }

                handle = cached;
                return true;
            }

            handle = null;
            return false;
        }

        public bool Return(ViewConfig config, IAssetHandle<GameObject> handle)
        {
            if (config == null || !IsReusable(handle))
            {
                return false;
            }

            if (!byConfig.TryGetValue(config, out Stack<IAssetHandle<GameObject>> pool))
            {
                pool = RegisterPool(config);
            }

            if (pool.Count >= maxPerView)
            {
                return false;
            }

            pool.Push(handle);
            return true;
        }

        private Stack<IAssetHandle<GameObject>> RegisterPool(ViewConfig config)
        {
            Stack<IAssetHandle<GameObject>> created = new Stack<IAssetHandle<GameObject>>();
            byConfig[config] = created;
            return created;
        }

        private bool IsReusable(IAssetHandle<GameObject> handle)
        {
            if (handle == null || handle.IsReleased)
            {
                return false;
            }

            return handle.State != AssetHandleState.Faulted;
        }
    }
}
