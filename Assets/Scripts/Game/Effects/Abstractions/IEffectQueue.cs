namespace Scaffold.Effects
{
    public interface IEffectQueue
    {
        public bool Running { get; }
        void QueueEffect(Effect effect);
    }
}