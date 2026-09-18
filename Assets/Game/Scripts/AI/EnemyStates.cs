using UnityEngine;

namespace ShapeCastle.AI
{
    internal abstract class EnemyStateBase : IEnemyState
    {
        protected readonly EnemyController Enemy;
        protected readonly EnemyStateMachine StateMachine;

        protected EnemyStateBase(EnemyController enemy, EnemyStateMachine stateMachine)
        {
            Enemy = enemy;
            StateMachine = stateMachine;
        }

        public abstract EnemyState Id { get; }

        public virtual void Enter()
        {
        }

        public abstract void Tick();

        public virtual void Exit()
        {
        }
    }

    internal sealed class EnemyMovingState : EnemyStateBase
    {
        public EnemyMovingState(EnemyController enemy, EnemyStateMachine stateMachine)
            : base(enemy, stateMachine)
        {
        }

        public override EnemyState Id => EnemyState.Moving;

        public override void Enter()
        {
            Enemy.BeginWandering();
        }

        public override void Tick()
        {
            if (Enemy.HasValidTarget)
            {
                Vector2 targetOffset = Enemy.GetTargetOffset();
                if (Enemy.IsTargetInsideAttackDistance(targetOffset))
                {
                    StateMachine.ChangeState(EnemyState.Attacking);
                    return;
                }

                if (Enemy.IsTargetInsideTrackingArea(targetOffset))
                {
                    StateMachine.ChangeState(EnemyState.Tracking);
                    return;
                }
            }

            Enemy.TickWandering();
        }
    }

    internal sealed class EnemyTrackingState : EnemyStateBase
    {
        public EnemyTrackingState(EnemyController enemy, EnemyStateMachine stateMachine)
            : base(enemy, stateMachine)
        {
        }

        public override EnemyState Id => EnemyState.Tracking;

        public override void Tick()
        {
            if (!Enemy.HasValidTarget)
            {
                StateMachine.ChangeState(EnemyState.Moving);
                return;
            }

            Vector2 targetOffset = Enemy.GetTargetOffset();
            if (Enemy.IsTargetInsideAttackDistance(targetOffset))
            {
                StateMachine.ChangeState(EnemyState.Attacking);
                return;
            }

            if (!Enemy.IsTargetInsideTrackingArea(targetOffset))
            {
                StateMachine.ChangeState(EnemyState.Moving);
                return;
            }

            Enemy.TickTracking(targetOffset);
        }
    }

    internal sealed class EnemyAttackingState : EnemyStateBase
    {
        public EnemyAttackingState(EnemyController enemy, EnemyStateMachine stateMachine)
            : base(enemy, stateMachine)
        {
        }

        public override EnemyState Id => EnemyState.Attacking;

        public override void Tick()
        {
            if (!Enemy.HasValidTarget)
            {
                StateMachine.ChangeState(EnemyState.Moving);
                return;
            }

            Vector2 targetOffset = Enemy.GetTargetOffset();
            if (!Enemy.IsTargetInsideAttackDistance(targetOffset))
            {
                EnemyState nextState = Enemy.IsTargetInsideTrackingArea(targetOffset)
                    ? EnemyState.Tracking
                    : EnemyState.Moving;
                StateMachine.ChangeState(nextState);
                return;
            }

            Enemy.TickAttacking(targetOffset);
        }
    }
}
