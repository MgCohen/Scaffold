using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Scaffold.Effects
{
    public class CommandDirector : MonoBehaviour, ICommands
    {
        private ICommandQueue queue;
        private IGameEvents events;


        private void Awake()
        {
            events = new GameEvents();
        }

        public Task Execute(Command command)
        {
            queue.QueueCommand(command);
            return Task.CompletedTask;
        }

        public IEventSubscription SubscribeTo<T>(Func<T,Task> callback) where T: Command
        {
            return events.SubscribeTo<T>(callback);
        }

        //queue commands for execution
        //execute commands
        //propagate commands
    }
}
