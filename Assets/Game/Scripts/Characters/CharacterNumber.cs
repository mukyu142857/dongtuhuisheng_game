using System;
using UnityEngine;

namespace ShapeCastle.Characters
{
    /// <summary>
    /// Shared numeric life value for players and enemies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAvatarView))]
    public sealed class CharacterNumber : MonoBehaviour
    {
        public const int MinimumAliveValue = 1;
        public const int MaximumValue = 666;

        [SerializeField, Range(MinimumAliveValue, MaximumValue)] private int initialValue = 5;
        [SerializeField] private PlayerAvatarView avatarView = null;

        private int currentValue;
        private bool isDamageImmune;
        private CharacterRespawnController respawnController;

        public event Action<int> ValueChanged;
        public event Action Eliminated;

        public int CurrentValue => currentValue;
        public bool IsEliminated => currentValue <= 0;
        public bool IsDamageImmune => isDamageImmune;

        private void Awake()
        {
            if (avatarView == null)
            {
                avatarView = GetComponent<PlayerAvatarView>();
            }

            ResetValue();
            respawnController = GetComponent<CharacterRespawnController>();
            if (respawnController == null)
            {
                respawnController = gameObject.AddComponent<CharacterRespawnController>();
            }
        }

        private void OnValidate()
        {
            initialValue = Mathf.Clamp(initialValue, MinimumAliveValue, MaximumValue);
            if (avatarView == null)
            {
                avatarView = GetComponent<PlayerAvatarView>();
            }

            if (!Application.isPlaying && avatarView != null)
            {
                avatarView.SetNumberText(initialValue.ToString());
            }
        }

        public bool ApplyHit(int amount = 1)
        {
            if (amount <= 0 || IsEliminated || isDamageImmune)
            {
                return false;
            }

            currentValue = Mathf.Max(0, currentValue - amount);
            RefreshView();
            ValueChanged?.Invoke(currentValue);

            if (currentValue == 0)
            {
                HandleElimination();
            }

            return true;
        }

        public void SetDamageImmunity(bool immune)
        {
            isDamageImmune = immune;
        }

        public bool AddValue(int amount)
        {
            if (amount <= 0 || IsEliminated)
            {
                return false;
            }

            long increasedValue = (long)currentValue + amount;
            currentValue = increasedValue > MaximumValue
                ? MaximumValue
                : (int)increasedValue;
            RefreshView();
            ValueChanged?.Invoke(currentValue);
            return true;
        }

        public bool SetCurrentValue(int newValue)
        {
            if (IsEliminated)
            {
                return false;
            }

            currentValue = Mathf.Clamp(newValue, 0, MaximumValue);
            RefreshView();
            ValueChanged?.Invoke(currentValue);

            if (currentValue == 0)
            {
                HandleElimination();
            }

            return true;
        }

        public void ResetValue()
        {
            isDamageImmune = false;
            currentValue = Mathf.Clamp(initialValue, MinimumAliveValue, MaximumValue);
            RefreshView();
            ValueChanged?.Invoke(currentValue);
        }

        public void ConfigureInitialValue(int newInitialValue)
        {
            initialValue = Mathf.Clamp(newInitialValue, MinimumAliveValue, MaximumValue);
            ResetValue();
        }

        private void RefreshView()
        {
            if (avatarView != null)
            {
                avatarView.SetNumberText(currentValue.ToString());
            }
        }

        private void HandleElimination()
        {
            isDamageImmune = false;
            Eliminated?.Invoke();

            if (respawnController == null)
            {
                respawnController = GetComponent<CharacterRespawnController>();
            }

            if (respawnController != null)
            {
                respawnController.BeginRespawn();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
