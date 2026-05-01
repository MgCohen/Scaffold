using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scaffold.Entities
{
    public class EntityBehaviorRunner<TData, TInput> : MonoBehaviour where TData : EntityComponent
    {
        public TData Entity => entityData;

        [SerializeField]
        [FormerlySerializedAs("Entity")]
        [FormerlySerializedAs("playerCore")]
        [FormerlySerializedAs("Player")]
        private TData entityData;

        [SerializeField]
        [FormerlySerializedAs("inputProvider")]
        private MonoBehaviour inputProviderBehaviour;

        [SerializeField]
        private List<MonoBehaviour> behaviorComponents = new List<MonoBehaviour>();

        private readonly List<IEntityBehavior<TData, TInput>> behaviors = new List<IEntityBehavior<TData, TInput>>();

        private IEntityFrameInputProvider<TInput> inputProvider;

        private IEntityBehavior<TData, TInput> lastExecutedBehavior;

        private void Awake()
        {
            inputProvider = inputProviderBehaviour as IEntityFrameInputProvider<TInput>;

            behaviors.Clear();
            for (int i = 0; i < behaviorComponents.Count; i++)
            {
                if (behaviorComponents[i] is IEntityBehavior<TData, TInput> b)
                {
                    behaviors.Add(b);
                }
            }
        }

        private void Update()
        {
            if (entityData == null)
            {
                return;
            }

            if (ShouldRunTick() == false)
            {
                return;
            }

            float dt = Time.deltaTime;
            TInput input = inputProvider != null ? inputProvider.GetFrameInput() : default;
            IEntityBehavior<TData, TInput> winner = FindWinningBehavior(in input);
            UpdateActiveBehavior(winner, in input, dt);
        }

        protected virtual bool ShouldRunTick()
        {
            return true;
        }

        private IEntityBehavior<TData, TInput> FindWinningBehavior(in TInput input)
        {
            for (int i = 0; i < behaviors.Count; i++)
            {
                if (behaviors[i].TryAcceptControl(entityData, in input))
                {
                    return behaviors[i];
                }
            }

            return null;
        }

        private void UpdateActiveBehavior(IEntityBehavior<TData, TInput> winner, in TInput input, float dt)
        {
            if (winner != lastExecutedBehavior)
            {
                lastExecutedBehavior?.OnQuit(entityData);
                lastExecutedBehavior = winner;
            }

            if (winner != null)
            {
                winner.Execute(entityData, in input, dt);
            }
        }

        public void ForceQuitActiveBehavior()
        {
            if (lastExecutedBehavior != null)
            {
                lastExecutedBehavior.OnQuit(entityData);
                lastExecutedBehavior = null;
            }
        }

        public void AssignInputProvider(IEntityFrameInputProvider<TInput> provider)
        {
            if (provider == null)
            {
                throw new System.ArgumentNullException(nameof(provider));
            }

            inputProvider = provider;
            inputProviderBehaviour = provider as MonoBehaviour;
        }
    }
}
