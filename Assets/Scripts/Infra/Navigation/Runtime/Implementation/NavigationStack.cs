
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scaffold.Navigation
{
    internal class NavigationStack
    {
        public int Count => stack.Count;
        public IView CurrentView => CurrentPoint?.View;
        public NavigationPoint CurrentPoint { get; private set; }
        public NavigationPoint PreviousPoint => Count <= 1 ? null : stack[^2];
        private List<NavigationPoint> stack = new List<NavigationPoint>();

        public NavigationPoint Get<T>()
        {
            return Get(typeof(T));
        }

        public NavigationPoint Get(Type screenType)
        {
            return stack.LastOrDefault(point => screenType.IsAssignableFrom(point.View.GetType()) || screenType.IsAssignableFrom(point.ViewModel.GetType()));
        }

        public NavigationPoint Get(IView screen)
        {
            return stack.LastOrDefault(point => point.View == screen);
        }

        public NavigationPoint Get(IViewController controller)
        {
            return stack.LastOrDefault(point => point.ViewModel == controller);
        }

        public List<NavigationPoint> GetAllStackedScreens(Func<NavigationPoint, bool> filter = null)
        {
            filter ??= (s) => true;
            return stack.Where(s => filter.Invoke(s)).ToList();
        }

        public List<IView> GetAllScreens(Func<NavigationPoint, bool> filter)
        {
            return GetAllStackedScreens(filter).Select(s => s.View).ToList();
        }

        public void AddToStack(NavigationPoint point)
        {
            if (point != null)
            {
                stack.Add(point);
                CurrentPoint = point;
            }
        }

        public void RemoveFromStack(NavigationPoint point)
        {
            if (point != null)
            {
                stack.Remove(point);
                if (CurrentPoint == point)
                {
                    CurrentPoint = stack.LastOrDefault();
                }
            }
        }

        public int GetPointDepth(NavigationPoint point)
        {
            int index = stack.IndexOf(point);
            if (index != 0)
                return Mathf.Max(index * 10, stack[index - 1].Depth + 10);
            else
            {
                return 0;
            }
        }

        public void ClearStack()
        {
            stack.Clear();
        }
    }

}
