using System;
using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    /// <summary>
    /// Non-generic base so the state machine can be stored without knowing T.
    /// </summary>
    public abstract class StateBase
    {
        public abstract void Enter();
        public abstract void Execute();
        public abstract void Exit();
    }

    /// <summary>
    /// Typed state with a back-reference to its owning context.
    /// </summary>
    public abstract class State<T> : StateBase where T : class
    {
        protected T Context { get; private set; }
        protected StateMachine<T> Machine { get; private set; }

        public void Init(T context, StateMachine<T> machine)
        {
            Context = context;
            Machine = machine;
        }

        public override void Enter() { }
        public override void Execute() { }
        public override void Exit() { }
    }

    /// <summary>
    /// Lightweight finite state machine. States are cached by type and reused.
    /// </summary>
    public sealed class StateMachine<T> where T : class
    {
        private readonly T _context;
        private readonly Dictionary<Type, State<T>> _stateCache = new Dictionary<Type, State<T>>();

        public StateBase CurrentState { get; private set; }
        public Type CurrentStateType { get; private set; }
        public float TimeInState { get; private set; }

        public StateMachine(T context)
        {
            _context = context;
        }

        /// <summary>
        /// Register a state instance. If not called before a transition, the
        /// machine will create instances via Activator.
        /// </summary>
        public void RegisterState<TState>(TState instance) where TState : State<T>
        {
            Type key = typeof(TState);
            instance.Init(_context, this);
            _stateCache[key] = instance;
        }

        /// <summary>
        /// Transition to a new state. If the machine is already in that state, nothing happens.
        /// </summary>
        public void ChangeState<TState>() where TState : State<T>, new()
        {
            Type key = typeof(TState);
            if (CurrentStateType == key) return;

            CurrentState?.Exit();

            if (!_stateCache.TryGetValue(key, out State<T> next))
            {
                next = new TState();
                next.Init(_context, this);
                _stateCache[key] = next;
            }

            CurrentState = next;
            CurrentStateType = key;
            TimeInState = 0f;
            CurrentState.Enter();
        }

        /// <summary>
        /// Must be called every frame (usually from MonoBehaviour.Update).
        /// </summary>
        public void Tick()
        {
            TimeInState += Time.deltaTime;
            CurrentState?.Execute();
        }
    }
}
