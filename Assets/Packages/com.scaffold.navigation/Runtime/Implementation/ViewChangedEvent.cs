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
    public record ViewChangedEvent : ContextEvent
    {
        [TypeReferenceFilter(typeof(IViewController))]
        [SerializeField] private TypeReference targetType;
        public ViewChangedEvent()
        {
        }
        public ViewChangedEvent(IViewController from, IViewController to)
        {
            ValidateEndpoints(from, to);
            this.From = from;
            this.To = to;
            targetType = new TypeReference(to?.GetType());
        }
        public IViewController To { get; }
        public IViewController From { get; }

        public TypeReference TargetType => targetType;

        private void ValidateEndpoints(IViewController from, IViewController to)
        {
            if (from == null && to == null)
            {
                throw new ArgumentException("At least one endpoint must be set.");
            }
        }
    }
}



