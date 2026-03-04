using System.Threading.Tasks;

namespace Scaffold.Effects
{
    public interface IEffectExecutor
    {
        Task ExecuteEffect(Effect effect);

        Task<bool> ValidateEffect(Effect effect);
    }
}
