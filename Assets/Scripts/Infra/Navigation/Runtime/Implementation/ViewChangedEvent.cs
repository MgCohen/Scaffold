using System;
using Scaffold.Events;
using Scaffold.Types;
using UnityEngine;

namespace Scaffold.Navigation
{
    public record ViewChangedEvent : ContextEvent
    {
        [TypeReferenceFilter(typeof(IViewController))]
        [SerializeField] private TypeReference targetType;
        public ViewChangedEvent()
        {
        }
        public ViewChangedEvent(IViewController from, IViewController to)
        {
            this.From = from;
            this.To = to;
            targetType = new TypeReference(to?.GetType());
        }
        public IViewController To { get; }
        public IViewController From { get; }

        public TypeReference TargetType => targetType;
    }
}
