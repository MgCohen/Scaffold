using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public class EffectExecutor : IEffectExecutor
    {
        public EffectExecutor(ICommands commands)
        {
            this.commands = commands;
        }

        private ICommands commands;

        public async Task ExecuteEffect(Effect effect)
        {
            EffectRunner runner = new EffectRunner(effect, null, commands);
            bool valid = await runner.Validate();
            if (!valid)
            {
                return;
            }
            await CallPreEvents(effect);
            await runner.Run();
            await CallPostEvents(effect);
        }

        public async Task<bool> ValidateEffect(Effect effect)
        {
            return await effect.Validate();
        }

        private async Task CallPreEvents(Effect effect)
        {
            await commands.Execute(new StartEffectExecution(effect));
        }

        private async Task CallPostEvents(Effect effect)
        {
            await commands.Execute(new FinishEffectExecution(effect));
        }
    }

    public class StartEffectExecution : Command
    {
        private Effect effect;

        public StartEffectExecution(Effect effect)
        {
            this.effect = effect;
        }

        public override Task Execute()
        {
            throw new NotImplementedException();
        }
    }

    public class FinishEffectExecution : Command
    {
        private Effect effect;

        public FinishEffectExecution(Effect effect)
        {
            this.effect = effect;
        }

        public override Task Execute()
        {
            throw new NotImplementedException();
        }
    }

    //use redux state or mvvm?
    //Redux for state(model)
    //MVVM for ViewModel
}
