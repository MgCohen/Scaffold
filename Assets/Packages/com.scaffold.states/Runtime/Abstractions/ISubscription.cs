using System;

namespace Scaffold.States
{
    public interface ISubscription
    {
        Type GetSubscriptionType();

        void Notify(IReference reference, BaseState state, StateChangeEvent changeEvent);
    }
}
