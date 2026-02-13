using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.Effects
{
    public class EffectDirector
    {
        public EffectDirector(ICommands commands)
        {
            this.commands = commands;
            this.executor = new EffectExecutor(commands);
            this.queue = new EffectQueue(executor);
        }

        private ICommands commands;
        private IEffectExecutor executor;
        private IEffectQueue queue;

        public void Execute(Effect effect)
        {
            EffectContext context = new EffectContext();
            Execute(effect, context);
        }

        public void Execute(Effect effect, EffectContext context)
        {
            queue.QueueEffect(effect);
        }

        public async Task<bool> Validate(Effect effect, EffectContext context)
        {
            return await executor.ValidateEffect(effect);
        }
    }

    [Serializable]
    public class EntryPoint
    {

    }
}