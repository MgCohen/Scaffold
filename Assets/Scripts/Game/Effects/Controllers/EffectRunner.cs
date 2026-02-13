using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public class EffectRunner
    {
        public EffectRunner(Effect effect, EffectContext context, ICommands commands)
        {
            this.effect = effect;
            this.context = context;
            this.commands = commands;
        }

        private Effect effect;
        private EffectContext context;
        private ICommands commands;

        public async Task Run()
        {
            await effect.Execute();
        }

        public async Task<bool> Validate()
        {
            return await effect.Validate();
        }

        protected async Task<T> ExecuteCommand<T>(T command) where T : Command
        {
            await commands.Execute(command);
            return command;
        }
    }

    //use redux state or mvvm?
    //Redux for state(model)
    //MVVM for ViewModel
}