using System;
using UnityEngine;

namespace ShapeCastle.Characters
{
    public enum PlayerSkillState
    {
        Ready,
        Executing,
        Cooldown
    }

    /// <summary>
    /// A skill delegate returns true only when the requested skill was
    /// successfully started.
    /// </summary>
    public delegate bool PlayerSkillAction();

    /// <summary>
    /// Coordinates the player's shared skill flow: ready, executing, then a
    /// short global cooldown. Individual skills may still keep their own
    /// longer cooldowns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkillStateMachine : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float skillCooldown = 0.2f;

        [Header("Runtime (Read Only)")]
        [SerializeField] private PlayerSkillState currentState = PlayerSkillState.Ready;

        private float cooldownEndTime;

        public event Action<PlayerSkillState> StateChanged;

        public PlayerSkillState CurrentState => currentState;
        public bool IsReady => currentState == PlayerSkillState.Ready;
        public float CooldownDuration => skillCooldown;
        public float RemainingCooldown => currentState == PlayerSkillState.Cooldown
            ? Mathf.Max(0f, cooldownEndTime - Time.time)
            : 0f;

        private void Update()
        {
            if (currentState == PlayerSkillState.Cooldown && Time.time >= cooldownEndTime)
            {
                ChangeState(PlayerSkillState.Ready);
            }
        }

        private void OnDisable()
        {
            cooldownEndTime = 0f;
            ChangeState(PlayerSkillState.Ready);
        }

        private void OnValidate()
        {
            skillCooldown = Mathf.Max(0f, skillCooldown);
        }

        public bool TryStartSkill(PlayerSkillAction skillAction)
        {
            if (!IsReady || skillAction == null)
            {
                return false;
            }

            ChangeState(PlayerSkillState.Executing);

            bool started;
            try
            {
                started = skillAction.Invoke();
            }
            catch
            {
                ChangeState(PlayerSkillState.Ready);
                throw;
            }

            if (!started)
            {
                ChangeState(PlayerSkillState.Ready);
            }

            return started;
        }

        public bool CompleteCurrentSkill()
        {
            if (currentState != PlayerSkillState.Executing)
            {
                return false;
            }

            if (skillCooldown <= 0f)
            {
                ChangeState(PlayerSkillState.Ready);
                return true;
            }

            cooldownEndTime = Time.time + skillCooldown;
            ChangeState(PlayerSkillState.Cooldown);
            return true;
        }

        public bool CancelCurrentSkill()
        {
            if (currentState != PlayerSkillState.Executing)
            {
                return false;
            }

            ChangeState(PlayerSkillState.Ready);
            return true;
        }

        private void ChangeState(PlayerSkillState nextState)
        {
            if (currentState == nextState)
            {
                return;
            }

            currentState = nextState;
            StateChanged?.Invoke(currentState);
        }
    }
}
