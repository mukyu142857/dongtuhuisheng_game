using ShapeCastle.Characters;
using ShapeCastle.Weapons;
using UnityEngine;

namespace ShapeCastle.AI
{
    public enum EnemyState
    {
        Moving,
        Tracking,
        Attacking
    }

    /// <summary>
    /// Owns the enemy data and components. Behaviour decisions live in the
    /// three state classes and are coordinated by EnemyStateMachine.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class EnemyController : MonoBehaviour, IMovementSpeedProvider
    {
        private static readonly Vector2[] WanderDirections =
        {
            Vector2.up,
            Vector2.down,
            Vector2.left,
            Vector2.right,
            new Vector2(1f, 1f).normalized,
            new Vector2(1f, -1f).normalized,
            new Vector2(-1f, 1f).normalized,
            new Vector2(-1f, -1f).normalized
        };

        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private MeleeWeaponController weapon;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 1f;
        [SerializeField, Min(0.1f)] private float wanderDirectionInterval = 2f;

        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float trackingAreaSize = 8f;
        [SerializeField, Min(0.1f)] private float attackDistance = 2f;
        [SerializeField, Min(0.1f)] private float attackInterval = 0.8f;

        [Header("Runtime (Read Only)")]
        [SerializeField] private EnemyState currentState = EnemyState.Moving;

        private Rigidbody2D body;
        private EnemyStateMachine stateMachine;
        private Vector2 desiredMoveDirection;
        private Vector2 wanderDirection = Vector2.right;
        private float nextWanderDirectionTime;
        private float nextAttackTime;

        public EnemyState CurrentState => stateMachine != null
            ? stateMachine.CurrentStateId
            : currentState;
        public float MoveSpeed => moveSpeed;

        internal bool HasValidTarget
        {
            get
            {
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    return false;
                }

                CharacterNumber targetNumber = target.GetComponent<CharacterNumber>();
                return targetNumber == null || !targetNumber.IsEliminated;
            }
        }

        private void Awake()
        {
            CacheAndConfigureComponents();
            ResolveReferences();
            CreateStateMachine();
        }

        private void Reset()
        {
            CacheAndConfigureComponents();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            wanderDirectionInterval = Mathf.Max(0.1f, wanderDirectionInterval);
            trackingAreaSize = Mathf.Max(0.1f, trackingAreaSize);
            attackDistance = Mathf.Max(0.1f, attackDistance);
            attackInterval = Mathf.Max(0.1f, attackInterval);
            CacheAndConfigureComponents();
        }

        private void Update()
        {
            if (!HasValidTarget || weapon == null)
            {
                ResolveReferences();
            }

            if (stateMachine == null)
            {
                CreateStateMachine();
            }

            stateMachine.Tick();
            currentState = stateMachine.CurrentStateId;
        }

        private void FixedUpdate()
        {
            if (body != null)
            {
                body.velocity = desiredMoveDirection * moveSpeed;
            }
        }

        private void OnDisable()
        {
            desiredMoveDirection = Vector2.zero;
            if (body != null)
            {
                body.velocity = Vector2.zero;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetWeapon(MeleeWeaponController newWeapon)
        {
            weapon = newWeapon;
            if (weapon != null)
            {
                weapon.SetAcceptPlayerInput(false);
            }
        }

        internal Vector2 GetTargetOffset()
        {
            return HasValidTarget ? (Vector2)target.position - body.position : Vector2.zero;
        }

        internal bool IsTargetInsideTrackingArea(Vector2 targetOffset)
        {
            float halfTrackingArea = trackingAreaSize * 0.5f;
            return Mathf.Abs(targetOffset.x) <= halfTrackingArea
                && Mathf.Abs(targetOffset.y) <= halfTrackingArea;
        }

        internal bool IsTargetInsideAttackDistance(Vector2 targetOffset)
        {
            return targetOffset.sqrMagnitude <= attackDistance * attackDistance;
        }

        internal void BeginWandering()
        {
            ChooseNewWanderDirection();
        }

        internal void TickWandering()
        {
            if (Time.time >= nextWanderDirectionTime)
            {
                ChooseNewWanderDirection();
            }

            desiredMoveDirection = wanderDirection;
            weapon?.SetAimDirection(wanderDirection);
        }

        internal void TickTracking(Vector2 targetOffset)
        {
            desiredMoveDirection = targetOffset.normalized;
            weapon?.SetAimDirection(targetOffset);
        }

        internal void TickAttacking(Vector2 targetOffset)
        {
            desiredMoveDirection = Vector2.zero;
            weapon?.SetAimDirection(targetOffset);

            if (weapon != null && Time.time >= nextAttackTime && !weapon.IsAttacking)
            {
                weapon.BeginNormalAttack();
                nextAttackTime = Time.time + attackInterval;
            }
        }

        private void CreateStateMachine()
        {
            stateMachine = new EnemyStateMachine();
            stateMachine.AddState(new EnemyMovingState(this, stateMachine));
            stateMachine.AddState(new EnemyTrackingState(this, stateMachine));
            stateMachine.AddState(new EnemyAttackingState(this, stateMachine));
            stateMachine.Initialize(EnemyState.Moving);
            currentState = stateMachine.CurrentStateId;
        }

        private void ChooseNewWanderDirection()
        {
            int directionIndex = Random.Range(0, WanderDirections.Length);
            wanderDirection = WanderDirections[directionIndex];
            nextWanderDirectionTime = Time.time + wanderDirectionInterval;
        }

        private void ResolveReferences()
        {
            if (!HasValidTarget)
            {
                PlayerMovementController player = FindObjectOfType<PlayerMovementController>();
                if (player != null && player.gameObject.activeInHierarchy)
                {
                    target = player.transform;
                }
            }

            if (weapon == null)
            {
                weapon = GetComponentInChildren<MeleeWeaponController>(true);
            }

            if (weapon != null)
            {
                weapon.SetAcceptPlayerInput(false);
            }
        }

        private void CacheAndConfigureComponents()
        {
            body = GetComponent<Rigidbody2D>();
            BoxCollider2D hitbox = GetComponent<BoxCollider2D>();

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Dynamic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            }

            if (hitbox != null)
            {
                hitbox.isTrigger = false;
                hitbox.offset = Vector2.zero;
                hitbox.size = Vector2.one;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.7f);
            Gizmos.DrawWireCube(transform.position, new Vector3(trackingAreaSize, trackingAreaSize, 0f));
            Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, attackDistance);
        }
#endif
    }
}
