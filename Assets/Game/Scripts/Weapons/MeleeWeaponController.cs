using System;
using System.Collections.Generic;
using ShapeCastle.Characters;
using ShapeCastle.World;
using UnityEngine;

namespace ShapeCastle.Weapons
{
    public enum ArithmeticOperatorType
    {
        Subtract,
        Add,
        Multiply,
        Divide
    }

    public enum WeaponState
    {
        Held,
        Charging,
        Flying,
        Dropped
    }

    /// <summary>
    /// Controls aiming, melee attacks, charged throws and automatic pickup.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MeleeWeaponController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private ArithmeticOperatorType operatorType = ArithmeticOperatorType.Subtract;
        [SerializeField] private bool randomizeOperatorOnStart;

        [Header("Sensor")]
        [SerializeField] private Vector2 sensorSize = new Vector2(2f, 0.5f);
        [SerializeField, Min(0f)] private float weaponCenterDistance = 0.9f;
        [SerializeField] private LayerMask targetLayers = ~0;

        [Header("Control")]
        [SerializeField] private bool acceptPlayerInput = true;

        [Header("Normal Attack")]
        [SerializeField, Range(1f, 180f)] private float swingAngle = 60f;
        [SerializeField, Min(0.01f)] private float swingDuration = 0.18f;
        [SerializeField, Min(0f)] private float attackCooldown = 1.8f;

        [Header("Charged Throw")]
        [SerializeField, Min(0.05f)] private float maxChargeTime = 2f;
        [SerializeField, Min(0.01f)] private float throwSpeedMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float maximumThrowDistance = 8f;
        [SerializeField] private float spinSpeed = 900f;
        [SerializeField, Min(0f)] private float ownerCollisionIgnoreTime = 0.12f;
        [SerializeField, Min(0f)] private float minimumFlightTime = 0.25f;
        [SerializeField, Min(0.01f)] private float landingSpeedThreshold = 0.35f;

        private readonly HashSet<int> hitTargets = new HashSet<int>();
        private readonly List<Collider2D> ignoredOwnerColliders = new List<Collider2D>();

        private BoxCollider2D sensor;
        private Rigidbody2D ownerBody;
        private Rigidbody2D weaponBody;
        private WeaponHolder currentHolder;
        private CharacterNumber ownerNumber;
        private PlayerSkillStateMachine playerSkillStateMachine;
        private Camera worldCamera;
        private WeaponState state = WeaponState.Held;
        private float aimAngle;
        private float attackAimAngle;
        private float attackStartTime;
        private float nextAttackTime;
        private float chargeStartTime;
        private float flightStartTime;
        private float targetThrowDistance;
        private float travelledThrowDistance;
        private Vector2 lastFlightPosition;
        private Vector2 lastFlightVelocity;
        private Vector2 externalAimDirection = Vector2.right;
        private bool isAttacking;
        private PlayerSkillAction normalAttackSkill;
        private PlayerSkillAction throwSkill;

        public event Action<Collider2D> TargetHit;

        public WeaponState State => state;
        public ArithmeticOperatorType OperatorType => operatorType;
        public WeaponHolder CurrentHolder => currentHolder;
        public bool IsAttacking => isAttacking;
        public bool IsCharging => state == WeaponState.Charging;
        public float SwingAngle => swingAngle;
        public Vector2 SensorSize => sensorSize;
        public float WeaponCenterDistance => weaponCenterDistance;
        public Vector2 AimDirection
        {
            get
            {
                float radians = aimAngle * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }
        public float Charge01 => IsCharging
            ? Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime)
            : 0f;

        public void SetAcceptPlayerInput(bool acceptInput)
        {
            acceptPlayerInput = acceptInput;
        }

        public void SetOperatorType(ArithmeticOperatorType newOperatorType)
        {
            operatorType = newOperatorType;
            GetComponent<OperatorWeaponSymbolView>()?.Refresh();
        }

        public void SetRandomizeOperatorOnStart(bool shouldRandomize)
        {
            randomizeOperatorOnStart = shouldRandomize;
        }

        public void RandomizeOperatorForHolder()
        {
            operatorType = (ArithmeticOperatorType)UnityEngine.Random.Range(0, 4);
            ApplyStartingOperatorValue();
            GetComponent<OperatorWeaponSymbolView>()?.Refresh();
        }

        public bool TryReturnToHolder(WeaponHolder holder)
        {
            if (holder == null)
            {
                return false;
            }

            if (currentHolder != null && currentHolder != holder)
            {
                return false;
            }

            if (currentHolder == holder)
            {
                return true;
            }

            if (!holder.TryClaim(this))
            {
                return false;
            }

            AttachToHolder(holder);
            return true;
        }

        public void CancelCurrentAction()
        {
            isAttacking = false;
            hitTargets.Clear();

            if (state == WeaponState.Charging)
            {
                state = WeaponState.Held;
            }

            playerSkillStateMachine?.CancelCurrentSkill();

            if (state == WeaponState.Held)
            {
                UpdateAimDirection();
                ApplyRotation(aimAngle);
            }
        }

        public void SetAimDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            externalAimDirection = direction.normalized;
            aimAngle = Mathf.Atan2(externalAimDirection.y, externalAimDirection.x) * Mathf.Rad2Deg;

            if (state == WeaponState.Held && !isAttacking)
            {
                ApplyRotation(aimAngle);
            }
        }

        private void Awake()
        {
            if (randomizeOperatorOnStart)
            {
                operatorType = (ArithmeticOperatorType)UnityEngine.Random.Range(0, 4);
            }

            CacheAndConfigureComponents();
            normalAttackSkill = BeginNormalAttack;
            throwSkill = BeginCharging;

            currentHolder = GetComponentInParent<WeaponHolder>();
            if (currentHolder != null && currentHolder.TryClaim(this))
            {
                ownerBody = currentHolder.GetComponent<Rigidbody2D>();
                ownerNumber = currentHolder.GetComponent<CharacterNumber>();
                RefreshPlayerSkillStateMachine();
                state = WeaponState.Held;
                ApplyStartingOperatorValue();
            }
            else
            {
                state = WeaponState.Dropped;
            }

            UpdateAimDirection();
            ApplyRotation(aimAngle);
            GetComponent<OperatorWeaponSymbolView>()?.Refresh();
        }

        private void Reset()
        {
            CacheAndConfigureComponents();
        }

        private void OnValidate()
        {
            sensorSize.x = Mathf.Max(0.01f, sensorSize.x);
            sensorSize.y = Mathf.Max(0.01f, sensorSize.y);
            weaponCenterDistance = Mathf.Max(0f, weaponCenterDistance);
            swingAngle = Mathf.Clamp(swingAngle, 1f, 180f);
            swingDuration = Mathf.Max(0.01f, swingDuration);
            attackCooldown = Mathf.Max(swingDuration, attackCooldown);
            maxChargeTime = Mathf.Max(0.05f, maxChargeTime);
            throwSpeedMultiplier = Mathf.Max(0.01f, throwSpeedMultiplier);
            maximumThrowDistance = Mathf.Max(0f, maximumThrowDistance);
            ownerCollisionIgnoreTime = Mathf.Max(0f, ownerCollisionIgnoreTime);
            minimumFlightTime = Mathf.Max(0f, minimumFlightTime);
            landingSpeedThreshold = Mathf.Max(0.01f, landingSpeedThreshold);
            CacheAndConfigureComponents();
        }

        private void Update()
        {
            switch (state)
            {
                case WeaponState.Held:
                    UpdateHeldState();
                    break;
                case WeaponState.Charging:
                    UpdateChargingState();
                    break;
                case WeaponState.Flying:
                    UpdateFlyingState();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (state == WeaponState.Flying && weaponBody != null)
            {
                lastFlightVelocity = weaponBody.velocity;
            }
        }

        private void OnDisable()
        {
            isAttacking = false;
            hitTargets.Clear();
            RestoreOwnerCollisions();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTriggerContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Stay handles a target that is already inside the sensor when an
            // attack begins, and a player standing on a dropped pickup.
            HandleTriggerContact(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (state != WeaponState.Flying || collision == null)
            {
                return;
            }

            if (collision.collider.GetComponentInParent<ArenaGrid>() != null)
            {
                BounceFromWall(collision);
                return;
            }

            if (TryEquipOnContact(collision.collider))
            {
                return;
            }

            TryRegisterHit(collision.collider);
        }

        public bool BeginNormalAttack()
        {
            if (state != WeaponState.Held || isAttacking || Time.time < nextAttackTime)
            {
                return false;
            }

            UpdateAimDirection();
            attackAimAngle = aimAngle;
            attackStartTime = Time.time;
            float sharedSkillCooldown = playerSkillStateMachine != null
                ? playerSkillStateMachine.CooldownDuration
                : 0f;
            nextAttackTime = attackStartTime + attackCooldown + sharedSkillCooldown;
            isAttacking = true;
            hitTargets.Clear();
            ApplyRotation(attackAimAngle - swingAngle * 0.5f);
            return true;
        }

        public bool BeginCharging()
        {
            if (state != WeaponState.Held || isAttacking)
            {
                return false;
            }

            UpdateAimDirection();
            chargeStartTime = Time.time;
            state = WeaponState.Charging;
            hitTargets.Clear();
            return true;
        }

        public bool ThrowChargedWeapon()
        {
            if (state != WeaponState.Charging || currentHolder == null)
            {
                return false;
            }

            UpdateAimDirection();
            float charge = Charge01;
            targetThrowDistance = maximumThrowDistance * charge;
            IMovementSpeedProvider movementSpeedProvider =
                currentHolder.GetComponent<IMovementSpeedProvider>();
            float holderMoveSpeed = movementSpeedProvider != null
                ? movementSpeedProvider.MoveSpeed
                : 1f;
            float launchSpeed = holderMoveSpeed * throwSpeedMultiplier;
            float angleInRadians = aimAngle * Mathf.Deg2Rad;
            Vector2 launchDirection = new Vector2(Mathf.Cos(angleInRadians), Mathf.Sin(angleInRadians));

            WeaponHolder previousHolder = currentHolder;
            Collider2D[] holderColliders = previousHolder.GetComponentsInChildren<Collider2D>(true);
            previousHolder.Release(this);
            currentHolder = null;

            transform.SetParent(null, true);
            CreateOrConfigureFlightBody();
            IgnorePreviousOwnerCollisions(holderColliders);

            sensor.isTrigger = false;
            weaponBody.velocity = launchDirection * launchSpeed;
            weaponBody.angularVelocity = spinSpeed;
            lastFlightVelocity = weaponBody.velocity;
            flightStartTime = Time.time;
            travelledThrowDistance = 0f;
            lastFlightPosition = weaponBody.worldCenterOfMass;
            state = WeaponState.Flying;
            hitTargets.Clear();
            return true;
        }

        private void UpdateHeldState()
        {
            if (isAttacking)
            {
                UpdateNormalAttack();
                return;
            }

            UpdateAimDirection();
            ApplyRotation(aimAngle);

            if (!acceptPlayerInput)
            {
                return;
            }

            if (Input.GetMouseButtonDown(1))
            {
                TryStartPlayerSkill(throwSkill);
            }
            else if (Input.GetMouseButtonDown(0))
            {
                TryStartPlayerSkill(normalAttackSkill);
            }
        }

        private void UpdateChargingState()
        {
            UpdateAimDirection();
            ApplyRotation(aimAngle);

            if (Input.GetMouseButtonUp(1) || !Input.GetMouseButton(1))
            {
                if (ThrowChargedWeapon())
                {
                    CompletePlayerSkill();
                }
                else
                {
                    playerSkillStateMachine?.CancelCurrentSkill();
                }
            }
        }

        private void UpdateFlyingState()
        {
            if (weaponBody == null)
            {
                return;
            }

            Vector2 currentFlightPosition = weaponBody.worldCenterOfMass;
            travelledThrowDistance += Vector2.Distance(lastFlightPosition, currentFlightPosition);
            lastFlightPosition = currentFlightPosition;

            if (ownerBody != null && Time.time - flightStartTime >= ownerCollisionIgnoreTime)
            {
                RestoreOwnerCollisions();
                ownerBody = null;
            }

            bool reachedChargedDistance = travelledThrowDistance >= targetThrowDistance;
            bool stoppedByCollision = Time.time - flightStartTime >= minimumFlightTime
                && weaponBody.velocity.sqrMagnitude <= landingSpeedThreshold * landingSpeedThreshold;

            if (reachedChargedDistance || stoppedByCollision)
            {
                BecomeDroppedPickup();
            }
        }

        private void UpdateNormalAttack()
        {
            float normalizedTime = Mathf.Clamp01((Time.time - attackStartTime) / swingDuration);
            float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
            float angleOffset = Mathf.Lerp(-swingAngle * 0.5f, swingAngle * 0.5f, easedTime);
            ApplyRotation(attackAimAngle + angleOffset);

            if (normalizedTime >= 1f)
            {
                isAttacking = false;
                hitTargets.Clear();
                CompletePlayerSkill();
            }
        }

        private void BecomeDroppedPickup()
        {
            state = WeaponState.Dropped;
            hitTargets.Clear();
            RestoreOwnerCollisions();
            ownerBody = null;
            ownerNumber = null;

            sensor.isTrigger = true;
            weaponBody.velocity = Vector2.zero;
            weaponBody.angularVelocity = 0f;
            weaponBody.drag = 0f;
            weaponBody.angularDrag = 0f;
            weaponBody.bodyType = RigidbodyType2D.Kinematic;
            weaponBody.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        }

        private void HandleTriggerContact(Collider2D other)
        {
            if (state == WeaponState.Dropped)
            {
                TryPickup(other);
            }
            else if (state == WeaponState.Held && isAttacking)
            {
                TryRegisterHit(other);
            }
        }

        private void TryPickup(Collider2D other)
        {
            TryEquipOnContact(other);
        }

        private bool TryEquipOnContact(Collider2D other)
        {
            if (other == null || other.isTrigger)
            {
                return false;
            }

            WeaponHolder holder = other.GetComponentInParent<WeaponHolder>();
            if (holder == null || !holder.TryClaim(this))
            {
                return false;
            }

            AttachToHolder(holder);
            return true;
        }

        private void AttachToHolder(WeaponHolder holder)
        {
            RestoreOwnerCollisions();
            currentHolder = holder;
            ownerBody = holder.GetComponent<Rigidbody2D>();
            ownerNumber = holder.GetComponent<CharacterNumber>();
            RefreshPlayerSkillStateMachine();
            state = WeaponState.Held;
            isAttacking = false;
            hitTargets.Clear();
            nextAttackTime = Time.time + 0.1f;

            if (weaponBody != null)
            {
                weaponBody.velocity = Vector2.zero;
                weaponBody.angularVelocity = 0f;
                weaponBody.simulated = false;
                Destroy(weaponBody);
                weaponBody = null;
            }

            transform.SetParent(holder.WeaponSocket, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            sensor.isTrigger = true;
            sensor.offset = new Vector2(weaponCenterDistance, 0f);
            sensor.size = sensorSize;

            UpdateAimDirection();
            ApplyRotation(aimAngle);
        }

        private bool TryStartPlayerSkill(PlayerSkillAction skillAction)
        {
            if (skillAction == null)
            {
                return false;
            }

            return playerSkillStateMachine != null
                ? playerSkillStateMachine.TryStartSkill(skillAction)
                : skillAction.Invoke();
        }

        private void CompletePlayerSkill()
        {
            playerSkillStateMachine?.CompleteCurrentSkill();
        }

        private void RefreshPlayerSkillStateMachine()
        {
            playerSkillStateMachine = currentHolder != null
                ? currentHolder.GetComponent<PlayerSkillStateMachine>()
                : null;
        }

        private bool TryRegisterHit(Collider2D other)
        {
            if (other == null || other.isTrigger)
            {
                return false;
            }

            if ((targetLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return false;
            }

            Rigidbody2D targetBody = other.attachedRigidbody;
            if (targetBody == null || targetBody == ownerBody || targetBody == weaponBody)
            {
                return false;
            }

            int targetId = targetBody.gameObject.GetInstanceID();
            if (!hitTargets.Add(targetId))
            {
                return false;
            }

            CharacterNumber targetNumber = other.GetComponentInParent<CharacterNumber>();
            ApplyOperatorHit(targetNumber);
            TargetHit?.Invoke(other);
            return true;
        }

        private void ApplyStartingOperatorValue()
        {
            if (ownerNumber == null)
            {
                return;
            }

            switch (operatorType)
            {
                case ArithmeticOperatorType.Add:
                    ownerNumber.ConfigureInitialValue(1);
                    break;
                case ArithmeticOperatorType.Subtract:
                    ownerNumber.ConfigureInitialValue(4);
                    break;
                case ArithmeticOperatorType.Multiply:
                    ownerNumber.ConfigureInitialValue(2);
                    break;
                case ArithmeticOperatorType.Divide:
                    ownerNumber.ConfigureInitialValue(5);
                    break;
            }
        }

        private void ApplyOperatorHit(CharacterNumber targetNumber)
        {
            if (targetNumber == null || targetNumber.IsEliminated || targetNumber.IsDamageImmune)
            {
                return;
            }

            switch (operatorType)
            {
                case ArithmeticOperatorType.Add:
                    ApplyAddHit(targetNumber);
                    break;
                case ArithmeticOperatorType.Subtract:
                    ApplySubtractHit(targetNumber);
                    break;
                case ArithmeticOperatorType.Multiply:
                    ApplyMultiplyHit(targetNumber);
                    break;
                case ArithmeticOperatorType.Divide:
                    ApplyDivideHit(targetNumber);
                    break;
            }
        }

        private void ApplyAddHit(CharacterNumber targetNumber)
        {
            int targetValueBeforeHit = targetNumber.CurrentValue;
            int targetValueAfterHit = targetValueBeforeHit / 2;
            int damage = targetValueBeforeHit - targetValueAfterHit;

            if (targetNumber.ApplyHit(damage))
            {
                ownerNumber?.AddValue(targetValueBeforeHit);
            }
        }

        private void ApplySubtractHit(CharacterNumber targetNumber)
        {
            if (ownerNumber == null || ownerNumber.IsEliminated)
            {
                return;
            }

            int ownerValueBeforeHit = ownerNumber.CurrentValue;
            int targetValueBeforeHit = targetNumber.CurrentValue;
            int valueGained = targetValueBeforeHit / 2;

            if (targetNumber.ApplyHit(ownerValueBeforeHit) && valueGained > 0)
            {
                ownerNumber.AddValue(valueGained);
            }
        }

        private void ApplyMultiplyHit(CharacterNumber targetNumber)
        {
            if (ownerNumber == null || ownerNumber.IsEliminated)
            {
                return;
            }

            int ownerValueBeforeHit = ownerNumber.CurrentValue;
            int targetValueBeforeHit = targetNumber.CurrentValue;
            int multiplier = Mathf.Max(2, targetValueBeforeHit);
            long multipliedValue = (long)ownerValueBeforeHit * multiplier;
            int newOwnerValue = multipliedValue > CharacterNumber.MaximumValue
                ? CharacterNumber.MaximumValue
                : (int)multipliedValue;
            ownerNumber.SetCurrentValue(newOwnerValue);
        }

        private void ApplyDivideHit(CharacterNumber targetNumber)
        {
            if (ownerNumber == null || ownerNumber.IsEliminated)
            {
                return;
            }

            int ownerValueBeforeHit = ownerNumber.CurrentValue;
            int targetValueBeforeHit = targetNumber.CurrentValue;
            int targetValueAfterHit = targetValueBeforeHit / ownerValueBeforeHit;
            int damage = targetValueBeforeHit - targetValueAfterHit;
            int valueGained = damage / 2;

            if (targetNumber.ApplyHit(damage) && valueGained > 0)
            {
                ownerNumber.AddValue(valueGained);
            }
        }

        private void BounceFromWall(Collision2D collision)
        {
            if (weaponBody == null || collision.contactCount == 0)
            {
                return;
            }

            Vector2 incomingVelocity = lastFlightVelocity.sqrMagnitude > 0.0001f
                ? lastFlightVelocity
                : weaponBody.velocity;
            Vector2 wallNormal = collision.GetContact(0).normal;
            Vector2 reflectedVelocity = Vector2.Reflect(incomingVelocity, wallNormal);

            if (reflectedVelocity.sqrMagnitude > 0.0001f)
            {
                weaponBody.velocity = reflectedVelocity.normalized * incomingVelocity.magnitude;
                lastFlightVelocity = weaponBody.velocity;
            }
        }

        private void UpdateAimDirection()
        {
            if (!acceptPlayerInput)
            {
                aimAngle = Mathf.Atan2(externalAimDirection.y, externalAimDirection.x) * Mathf.Rad2Deg;
                return;
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (worldCamera == null)
            {
                return;
            }

            Vector3 mouseWorldPosition = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 aimDirection = (Vector2)mouseWorldPosition - (Vector2)transform.position;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            }
        }

        private void ApplyRotation(float angle)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void CreateOrConfigureFlightBody()
        {
            weaponBody = GetComponent<Rigidbody2D>();
            if (weaponBody == null)
            {
                weaponBody = gameObject.AddComponent<Rigidbody2D>();
            }

            weaponBody.simulated = true;
            weaponBody.bodyType = RigidbodyType2D.Dynamic;
            weaponBody.mass = 0.4f;
            weaponBody.gravityScale = 0f;
            weaponBody.drag = 0f;
            weaponBody.angularDrag = 0.15f;
            weaponBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            weaponBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            weaponBody.constraints = RigidbodyConstraints2D.None;
        }

        private void IgnorePreviousOwnerCollisions(IEnumerable<Collider2D> holderColliders)
        {
            ignoredOwnerColliders.Clear();
            foreach (Collider2D holderCollider in holderColliders)
            {
                if (holderCollider == null || holderCollider == sensor || holderCollider.isTrigger)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(sensor, holderCollider, true);
                ignoredOwnerColliders.Add(holderCollider);
            }
        }

        private void RestoreOwnerCollisions()
        {
            if (sensor != null)
            {
                foreach (Collider2D holderCollider in ignoredOwnerColliders)
                {
                    if (holderCollider != null)
                    {
                        Physics2D.IgnoreCollision(sensor, holderCollider, false);
                    }
                }
            }

            ignoredOwnerColliders.Clear();
        }

        private void CacheAndConfigureComponents()
        {
            sensor = GetComponent<BoxCollider2D>();
            if (sensor != null)
            {
                sensor.isTrigger = true;
                sensor.offset = new Vector2(weaponCenterDistance, 0f);
                sensor.size = sensorSize;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.25f, 0.12f, 0.8f);
            Gizmos.DrawWireCube(new Vector3(weaponCenterDistance, 0f, 0f), sensorSize);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
#endif
    }
}
