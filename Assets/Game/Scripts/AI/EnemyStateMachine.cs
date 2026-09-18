using System;
using System.Collections.Generic;

namespace ShapeCastle.AI
{
    internal interface IEnemyState
    {
        EnemyState Id { get; }

        void Enter();
        void Tick();
        void Exit();
    }

    /// <summary>
    /// Small reusable finite-state-machine that owns state transitions.
    /// </summary>
    internal sealed class EnemyStateMachine
    {
        private readonly Dictionary<EnemyState, IEnemyState> states =
            new Dictionary<EnemyState, IEnemyState>();

        private IEnemyState currentState;

        public EnemyState CurrentStateId => currentState != null
            ? currentState.Id
            : EnemyState.Moving;

        public void AddState(IEnemyState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            states[state.Id] = state;
        }

        public void Initialize(EnemyState initialState)
        {
            currentState = null;
            ChangeState(initialState);
        }

        public void ChangeState(EnemyState nextStateId)
        {
            if (currentState != null && currentState.Id == nextStateId)
            {
                return;
            }

            if (!states.TryGetValue(nextStateId, out IEnemyState nextState))
            {
                throw new InvalidOperationException($"Enemy state {nextStateId} has not been registered.");
            }

            currentState?.Exit();
            currentState = nextState;
            currentState.Enter();
        }

        public void Tick()
        {
            currentState?.Tick();
        }
    }
}
