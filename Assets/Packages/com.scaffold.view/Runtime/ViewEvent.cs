using Scaffold.Navigation.Contracts;
using Scaffold.MVVM.Binding;
using Scaffold.MVVM.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scaffold.MVVM
{
    [Serializable]
    public abstract class ViewEvent : IViewEvent
    {
        public ViewEvent(PointerEventData pointer)
        {
            if (pointer is null)
            {
                throw new ArgumentNullException(nameof(pointer));
            }
            pointerData = pointer;
        }

        public ViewEvent()
        {
        }

        public PointerEventData PointerData => pointerData;
        [SerializeField] private PointerEventData pointerData;

        public Transform Source => source;
        [SerializeField] private Transform source;
        public Transform Current => current;
        [SerializeField] private Transform current;
        public List<Transform> History => history;
        [SerializeField] private List<Transform> history = new();

        public Transform Consumer => consumer;
        [SerializeField] private Transform consumer;
        public bool IsConsumed => isConsumed;
        [SerializeField] private bool isConsumed;

        public int MaxRange => maxRange;
        [SerializeField] private int maxRange = -1;

        public void Consume()
        {
            GuardState();
            isConsumed = true;
            consumer = current;
            if (History.LastOrDefault() != Current)
            {
                History.Add(Current);
            }
            current = null;
        }

        public void Restore()
        {
            GuardState();
            isConsumed = false;
            current = null;
            consumer = null;
            History.Clear();
        }

        public void LogNext(Transform next)
        {
            if (next is null)
            {
                throw new ArgumentNullException(nameof(next));
            }
            GuardState();
            if (Current != null)
            {
                History.Add(Current);
            }
            current = next;
        }

        private void GuardState()
        {
            if (history == null)
            {
                history = new List<Transform>();
            }
        }
    }
}



