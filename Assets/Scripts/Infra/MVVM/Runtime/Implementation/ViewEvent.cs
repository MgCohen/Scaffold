using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Scaffold.MVVM
{
    [Serializable]
    public abstract class ViewEvent
    {
        public ViewEvent(PointerEventData pointer)
        {
            PointerData = pointer;
        }

        public ViewEvent()
        {
        }

        public PointerEventData PointerData { get; private set; }

        public Transform Source { get; private set; }
        public Transform Current { get; private set; }
        public List<Transform> History { get; private set; } = new List<Transform>();

        public Transform Consumer { get; private set; }
        public bool IsConsumed { get; private set; }

        public int maxRange { get; private set; } = -1;

        public void Consume()
        {
            IsConsumed = true;
            Consumer = Current;
            AddCurrentToHistoryIfNeeded();
            Current = null;
        }

        private void AddCurrentToHistoryIfNeeded()
        {
            if (History.LastOrDefault() != Current)
            {
                History.Add(Current);
            }
        }

        public void Restore()
        {
            IsConsumed = false;
            Current = null;
            Consumer = null;
            History.Clear();
        }

        public void LogNext(Transform next)
        {
            if (Current != null)
            {
                History.Add(Current);
            }
            Current = next;
        }
    }
}
