using ShapeCastle.AI;
using ShapeCastle.Weapons;
using UnityEngine;

namespace ShapeCastle.Characters
{
    /// <summary>
    /// E skill: damages every enemy in a short cone in front of the player.
    /// The skill is submitted to PlayerSkillStateMachine through a delegate.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSkillStateMachine))]
    public sealed class PlayerExecutionSkill : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode activationKey = KeyCode.E;

        [Header("Area")]
        [SerializeField, Min(0.1f)] private float range = 1f;
        [SerializeField, Range(1f, 360f)] private float coneAngle = 60f;

        [Header("Damage")]
        [SerializeField, Range(0f, 1f)] private float damagePercentage = 0.9f;
        [SerializeField, Min(1)] private int executeBelowValue = 2;
        [SerializeField, Min(0f)] private float skillCooldown = 2f;

        [Header("References")]
        [SerializeField] private PlayerSkillStateMachine skillStateMachine;
        [SerializeField] private MeleeWeaponController weapon;

        private PlayerSkillAction executionAction;
        private float nextAvailableTime;

        public float RemainingCooldown => Mathf.Max(0f, nextAvailableTime - Time.time);

        private void Awake()
        {
            ResolveReferences();
            executionAction = ExecuteInFront;
        }

        private void OnValidate()
        {
            range = Mathf.Max(0.1f, range);
            coneAngle = Mathf.Clamp(coneAngle, 1f, 360f);
            damagePercentage = Mathf.Clamp01(damagePercentage);
            executeBelowValue = Mathf.Max(1, executeBelowValue);
            skillCooldown = Mathf.Max(0f, skillCooldown);
            ResolveReferences();
        }

        private void Update()
        {
            if (Input.GetKeyDown(activationKey))
            {
                TryActivate();
            }
        }

        public bool TryActivate()
        {
            if (skillStateMachine == null)
            {
                ResolveReferences();
            }

            if (skillStateMachine == null || executionAction == null)
            {
                return false;
            }

            if (!skillStateMachine.TryStartSkill(executionAction))
            {
                return false;
            }

            skillStateMachine.CompleteCurrentSkill();
            return true;
        }

        private bool ExecuteInFront()
        {
            if (Time.time < nextAvailableTime)
            {
                return false;
            }

            Vector2 forward = GetForwardDirection();
            Vector2 origin = transform.position;
            float maxRangeSquared = range * range + 0.0001f;
            float halfConeAngle = coneAngle * 0.5f;

            EnemyController[] enemies = FindObjectsOfType<EnemyController>();
            foreach (EnemyController enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 targetOffset = (Vector2)enemy.transform.position - origin;
                Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
                Vector2 closestPoint = enemyCollider != null
                    ? enemyCollider.ClosestPoint(origin)
                    : (Vector2)enemy.transform.position;
                Vector2 closestOffset = closestPoint - origin;

                if (closestOffset.sqrMagnitude > maxRangeSquared
                    || Vector2.Angle(forward, targetOffset) > halfConeAngle)
                {
                    continue;
                }

                CharacterNumber targetNumber = enemy.GetComponent<CharacterNumber>();
                if (targetNumber == null || targetNumber.IsEliminated)
                {
                    continue;
                }

                int currentValue = targetNumber.CurrentValue;
                int damage = currentValue < executeBelowValue
                    ? currentValue
                    : Mathf.FloorToInt(currentValue * damagePercentage);

                if (damage > 0)
                {
                    targetNumber.ApplyHit(damage);
                }
            }

            nextAvailableTime = Time.time + skillCooldown;
            return true;
        }

        private Vector2 GetForwardDirection()
        {
            if (weapon == null)
            {
                weapon = GetComponentInChildren<MeleeWeaponController>(true);
            }

            return weapon != null ? weapon.AimDirection : Vector2.right;
        }

        private void ResolveReferences()
        {
            if (skillStateMachine == null)
            {
                skillStateMachine = GetComponent<PlayerSkillStateMachine>();
            }

            if (weapon == null)
            {
                weapon = GetComponentInChildren<MeleeWeaponController>(true);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector2 origin = transform.position;
            Vector2 forward = Application.isPlaying ? GetForwardDirection() : (Vector2)transform.right;
            float centerAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            Vector2 leftEdge = DirectionFromAngle(centerAngle + coneAngle * 0.5f);
            Vector2 rightEdge = DirectionFromAngle(centerAngle - coneAngle * 0.5f);

            Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.8f);
            Gizmos.DrawLine(origin, origin + leftEdge * range);
            Gizmos.DrawLine(origin, origin + rightEdge * range);

            const int segmentCount = 12;
            Vector2 previousPoint = origin + rightEdge * range;
            for (int segment = 1; segment <= segmentCount; segment++)
            {
                float angle = centerAngle - coneAngle * 0.5f + coneAngle * segment / segmentCount;
                Vector2 nextPoint = origin + DirectionFromAngle(angle) * range;
                Gizmos.DrawLine(previousPoint, nextPoint);
                previousPoint = nextPoint;
            }
        }

        private static Vector2 DirectionFromAngle(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
#endif
    }
}
