using System;

using UnityEngine;



namespace Scaffold.Entities

{

    [Serializable]

    public abstract class BaseEntityInstance<TDefinition> : IReadOnlyEntity<TDefinition> where TDefinition : IEntityDefinition

    {

        protected BaseEntityInstance()

        {

        }



        public InstanceId Id => id;



        [SerializeField] private InstanceId id;



        internal TDefinition Definition => definition;



        [SerializeField] protected TDefinition definition = default!;



        protected IEntityVariableStorage Storage { get; private set; } = default!;



        protected void Initialize(InstanceId instanceId, TDefinition entityDefinition, IEntityVariableStorage storage)

        {

            if (storage == null)

            {

                throw new ArgumentNullException(nameof(storage));

            }



            id = instanceId;

            definition = entityDefinition ?? throw new ArgumentNullException(nameof(entityDefinition));

            Storage = storage;

        }



        public T GetValue<T>(Variable key)

        {

            if (!TryResolve(key, out VariableValue av) || av == null)

            {

                throw new InvalidOperationException(

                    $"Variable '{key?.Key ?? "?"}' is not defined on this entity.");

            }



            if (av is IVariableValue<T> typed)

            {

                return typed.Get();

            }



            throw new InvalidCastException(

                $"Variable '{key?.Key ?? "?"}' has runtime payload type {av.GetType().Name} but {typeof(T).Name} was requested.");

        }



        public bool TryGetValue<T>(Variable key, out T value)

        {

            value = default!;

            if (!TryResolve(key, out VariableValue av) || av == null)

            {

                return false;

            }



            if (av is IVariableValue<T> typed)

            {

                value = typed.Get();

                return true;

            }



            return false;

        }



        public TVar GetVariable<TVar>(Variable key) where TVar : VariableValue

        {

            if (!TryResolve(key, out VariableValue av) || av == null)

            {

                throw new InvalidOperationException(

                    $"Variable '{key?.Key ?? "?"}' is not defined on this entity.");

            }



            if (av is TVar typed)

            {

                return typed;

            }



            throw new InvalidCastException(

                $"Variable '{key?.Key ?? "?"}' is {av.GetType().Name} but {typeof(TVar).Name} was requested.");

        }



        public bool TryGetVariable<TVar>(Variable key, out TVar value) where TVar : VariableValue

        {

            value = default!;

            if (!TryResolve(key, out VariableValue av) || av == null)

            {

                return false;

            }



            if (av is TVar typed)

            {

                value = typed;

                return true;

            }



            return false;

        }



        private bool TryResolve(Variable key, out VariableValue value)

        {

            return Storage.TryGetEffective(key, out value);

        }



        public IDisposable Subscribe(Variable key, Action<VariableValue> onChange)

        {

            if (key == null || onChange == null)

            {

                return EmptyDisposable.Instance;

            }



            return Storage.Subscribe(key, onChange);

        }



        public void Unsubscribe(Variable key, Action<VariableValue> onChange)

        {

            if (key == null || onChange == null)

            {

                return;

            }



            Storage.Unsubscribe(key, onChange);

        }



        public IDisposable SubscribeToVariableStructuralChanges(Action<VariableStructuralChange, Variable, VariableValue?> handler)

        {

            if (handler == null)

            {

                return EmptyDisposable.Instance;

            }



            return Storage.SubscribeToVariableStructuralChanges(handler);

        }

    }

}

