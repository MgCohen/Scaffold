using System;

namespace Scaffold.States
{
    public interface ISubscription
    {
        public Type GetSubscriptionType();
        public void Notify(IReference reference, BaseState state);
    }
}
