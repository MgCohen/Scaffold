using System;
using UnityEngine;

namespace Scaffold.Navigation
{
    public class ViewTransitionData
    {
        public NavigationPoint From { get; private set; }
        public NavigationPoint To { get; private set; }
        public bool CloseCurrent { get; private set; }

        public Func<Awaitable> ClosingSequence { get; set; }
        public Func<Awaitable> OpenningSequence { get; set; }
        public Func<Awaitable> HidingSequence { get; set; }

        public ViewTransitionData(NavigationPoint from, NavigationPoint to, bool closeCurrent)
        {
            this.From = from;
            this.To = to;
            this.CloseCurrent = closeCurrent;
        }
    }
}
