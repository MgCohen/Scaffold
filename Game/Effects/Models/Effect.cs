using System;
using System.Threading.Tasks;
using Unity.VisualScripting;

namespace Scaffold.Effects
{
    public abstract class Effect
    {
        public readonly static Effect NullEffect = new NullEffect();

        public abstract Task Execute();

        protected async Task<T> ExecuteCommand<T>(T command) where T : Command
        {
            await RunCommandEvents(command);
            await command.Execute();
            await RunCommandEvents(command);
            return command;
        }

        private async Task RunCommandEvents(Command command)
        {
            //run before/after triggers
            await Task.Delay(100);
        }

        public void Thing(EffectContext context)
        {
            if (context == null)
            {
                //do something
            }
            else
            {
                //do something else
            }

            //    var a = await ExecuteCommand(new ACommand());
            //    if (a != null)
            //    {
            //        var b = await ExecuteCommand(new BCommand());
            //    }
            //    else
            //    {
            //        var a2 = await ExecuteCommand(new ACommand());
            //    }
        }

        public abstract Task<bool> Validate();
    }

    public class NullEffect : Effect
    {
        public override Task Execute()
        {
            return Task.CompletedTask;
        }

        public override Task<bool> Validate()
        {
            return Task.FromResult(false);
        }
    }

    //context at execution
    //state at execution?
    //commands at creation
    //execution callback at creation
    //validation callback at creation
    //costs at creation


    //this would be the context object
    public class EffectFlow
    {
        public void ReadVariable(string key)
        {

        }

        public void WriteVariable(string key, object value)
        {

        }

        public void RunCommand(Command command)
        {

        }
    }

    namespace Testing
{
    public class EffectFlow
    {
        //list of previous effects
        //trigger effect
        //trigger entity
        //last effect
        //last entity
        //variables

        public T Execute<T>(T command) where T : Command
        {
            return command;
        }
    }

    public class Effect
    {
        public Effect RegisterEntryPoint<T>() where T : EntryPoint
        {
            return new Effect();
        }

        public Effect RegisterReaction<T>() where T : Command
        {
            return new Effect();
        }

        public Effect WithCondition(Func<bool> condition)
        {
            return this;
        }

        public Effect WithCost(Action cost)
        {
            return this;
        }

        public Effect WithEffect(Func<EffectFlow, Task> action)
        {
            return this;
        }
    }

    public class TestEffectHolder
    {
        public void Register()
        {
            RegisterEntryPoint<OnPlay>().WithCondition(() => true)
                                        .WithCost(() => { /*pay something*/ })
                                        .WithEffect(async (f) =>
                                        {
                                            var a = f.Execute(new ACommand());
                                            await Task.Delay(100);
                                            var b = f.Execute(new BCommand());
                                        });

            RegisterReaction<ACommand>().WithCost(() => { })
                                        .WithEffect(async (f) =>
                                        {
                                            await Task.Delay(100);
                                        });
        }

        public Effect RegisterEntryPoint<T>() where T : EntryPoint
        {
            return new Effect().RegisterEntryPoint<T>();
        }

        public Effect RegisterReaction<T>() where T : Command
        {
            return new Effect().RegisterReaction<T>();
        }
    }
    }
}
