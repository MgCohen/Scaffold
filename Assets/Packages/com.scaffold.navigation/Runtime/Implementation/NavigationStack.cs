using UnityEngine;
using Scaffold.Types;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Scaffold.Navigation.Contracts;
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
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            Type screenType = typeof(T);
            return stack.LastOrDefault(point =>
            {
                Type viewType = point.View.GetType();
                Type viewModelType = point.ViewModel.GetType();
                return screenType.IsAssignableFrom(viewType) || screenType.IsAssignableFrom(viewModelType);
            });
        }

        public NavigationPoint Get(Type screenType)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            return stack.LastOrDefault(point =>
            {
                Type viewType = point.View.GetType();
                Type viewModelType = point.ViewModel.GetType();
                return screenType.IsAssignableFrom(viewType) || screenType.IsAssignableFrom(viewModelType);
            });
        }

        public NavigationPoint Get(IView screen)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            return stack.LastOrDefault(point => point.View == screen);
        }

        public NavigationPoint Get(IViewController controller)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            return stack.LastOrDefault(point => point.ViewModel == controller);
        }

        public List<IView> GetAllScreens(Func<NavigationPoint, bool> filter)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            return GetAllStackedScreens(filter).Select(s => s.View).ToList();
        }

        public List<NavigationPoint> GetAllStackedScreens(Func<NavigationPoint, bool> filter = null)
        {
            filter ??= (s) => true;
            return stack.Where(s => filter.Invoke(s)).ToList();
        }

        public void AddToStack(NavigationPoint point)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            if (point != null)
            {
                stack.Add(point);
                CurrentPoint = point;
            }
        }

        public void RemoveFromStack(NavigationPoint point)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            if (point == null)
{
    return;
}
            stack.Remove(point);
            UpdateCurrentPointAfterRemoval(point);
        }

        private void UpdateCurrentPointAfterRemoval(NavigationPoint point)
        {
            if (CurrentPoint == point)
{
    CurrentPoint = stack.LastOrDefault();
}
        }

        public int GetPointDepth(NavigationPoint point)
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            int index = stack.IndexOf(point);
            if (index != 0)
            {
                return Mathf.Max(index * 10, stack[index - 1].Depth + 10);
            }
            else
            {
                return 0;
            }
        }

        public void ClearStack()
        {
            if (stack == null) throw new InvalidOperationException("Navigation stack is not initialized.");
            stack.Clear();
        }
    }

}






