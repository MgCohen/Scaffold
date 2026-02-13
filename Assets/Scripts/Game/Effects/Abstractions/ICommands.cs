using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public interface ICommands
    {
        public Task Execute(Command command);
    }
}
