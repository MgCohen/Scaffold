using UnityEngine;
using Scaffold.Types;
using Scaffold.Navigation.Contracts;
using Scaffold.Events.Contracts;
using Scaffold.Events;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace Scaffold.Navigation
{
    public class ViewTransitionData
    {
        public ViewTransitionData(NavigationPoint from, NavigationPoint to, bool closeCurrent)
        {
            EnsureTransitionPoints(from, to);
            this.From = from;
            this.To = to;
            this.CloseCurrent = closeCurrent;
        }

        public NavigationPoint From { get; private set; }
        public NavigationPoint To { get; private set; }
        public bool CloseCurrent { get; private set; }
        public Func<Task> ClosingSequence { get; set; }
        public Func<Task> OpenningSequence { get; set; }
        public Func<Task> HidingSequence { get; set; }

        private void EnsureTransitionPoints(NavigationPoint from, NavigationPoint to)
        {
            if (from == null && to == null)
            {
                throw new ArgumentException("At least one navigation point is required.");
            }
        }
    }
}


