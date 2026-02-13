using System.Collections.Generic;

namespace Scaffold.Effects
{
    public class EffectQueue : IEffectQueue
    {
        public EffectQueue(IEffectExecutor executor)
        {
            this.executor = executor;
        }

        public bool Running => effectsRunning > 0;
        private int effectsRunning = 0;

        private IEffectExecutor executor;

        public void QueueEffect(Effect effect)
        {
            Execute(effect);
        }

        private async void Execute(Effect effect)
        {
            effectsRunning++;
            await executor.ExecuteEffect(effect);
            effectsRunning--;
        }
    }
}
