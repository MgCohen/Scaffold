using System;
using System.Collections.Generic;
using Scaffold.Navigation.Contracts;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal sealed class NavigationViewInstanceBuffer
    {
        public NavigationViewInstanceBuffer(Transform viewHolder, int maxPerView = 2)
        {
            if (viewHolder == null)
{
    throw new ArgumentNullException(nameof(viewHolder));
}
            this.viewHolder = viewHolder;
            this.maxPerView = Math.Max(1, maxPerView);
        }

        private readonly Transform viewHolder;
        private readonly int maxPerView;
        private readonly Dictionary<ViewConfig, Stack<IView>> byConfig = new Dictionary<ViewConfig, Stack<IView>>();

        public bool TryTake(ViewConfig config, out IView view)
        {
            view = null;
            if (config == null) return false;
            if (!byConfig.TryGetValue(config, out Stack<IView> pool))
{
    view = null; return false;
}
            return TryTakeValid(pool, out view);
        }

        private bool TryTakeValid(Stack<IView> pool, out IView view)
        {
            while (pool.Count > 0)
            {
                if (TryPopValidView(pool, out view))
{
    return true;
}
            }
            view = null;
            return false;
        }

        private bool TryPopValidView(Stack<IView> pool, out IView view)
        {
            IView cached = pool.Pop();
            if (cached == null || cached.gameObject == null)
{
    view = null; return false;
}
            view = cached;
            return true;
        }

        public void Return(ViewConfig config, IView view)
        {
            if (config == null || view == null || view.gameObject == null) return;
            if (!byConfig.TryGetValue(config, out Stack<IView> pool)) pool = RegisterPool(config);
            if (pool.Count >= maxPerView)
            {
                UnityEngine.Object.Destroy(view.gameObject);
                return;
            }
            GameObject gameObject = view.gameObject;
            gameObject.transform.SetParent(viewHolder, false);
            gameObject.SetActive(false);
            pool.Push(view);
        }

        private Stack<IView> RegisterPool(ViewConfig config)
        {
            Stack<IView> created = new Stack<IView>();
            byConfig[config] = created;
            return created;
        }

    }
}


