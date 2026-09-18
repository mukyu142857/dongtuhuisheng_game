using System.Collections;
using System.Collections.Generic;
using ShapeCastle.AI;
using ShapeCastle.Weapons;
using ShapeCastle.World;
using UnityEngine;

namespace ShapeCastle.Characters
{
    /// <summary>
    /// Keeps an eliminated character alive as a scene object, then restores it
    /// on a random arena cell with temporary spawn protection.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterNumber), typeof(Rigidbody2D))]
    public sealed class CharacterRespawnController : MonoBehaviour
    {
        private struct BehaviourState
        {
            public MonoBehaviour Behaviour;
            public bool WasEnabled;
        }

        private struct RendererState
        {
            public Renderer Renderer;
            public bool WasEnabled;
        }

        private struct ColliderState
        {
            public Collider2D Collider;
            public bool WasEnabled;
        }

        [Header("Respawn")]
        [SerializeField, Min(0f)] private float respawnDelay = 2f;
        [SerializeField, Min(0f)] private float spawnProtectionDuration = 2f;
        [SerializeField, Min(0f)] private float minimumCharacterSeparation = 1.25f;
        [SerializeField, Min(1)] private int randomSpawnAttempts = 40;

        [Header("Protection Visual")]
        [SerializeField] private Color shieldColor = Color.white;
        [SerializeField, Min(0.1f)] private float shieldRadius = 0.7f;
        [SerializeField, Min(0.01f)] private float shieldLineWidth = 0.06f;
        [SerializeField, Range(12, 96)] private int shieldSegments = 48;

        private readonly List<BehaviourState> suspendedBehaviours = new List<BehaviourState>();
        private readonly List<RendererState> hiddenRenderers = new List<RendererState>();
        private readonly List<ColliderState> disabledColliders = new List<ColliderState>();

        private CharacterNumber characterNumber;
        private Rigidbody2D body;
        private ArenaGrid arena;
        private WeaponHolder weaponHolder;
        private MeleeWeaponController assignedWeapon;
        private Coroutine respawnRoutine;
        private bool bodyWasSimulated;
        private GameObject generatedShield;
        private Material generatedShieldMaterial;

        public bool IsRespawning => respawnRoutine != null;
        public float RespawnDelay => respawnDelay;
        public float SpawnProtectionDuration => spawnProtectionDuration;

        private void Awake()
        {
            ResolveReferences();
            SetShieldVisible(false);
        }

        private void OnValidate()
        {
            respawnDelay = Mathf.Max(0f, respawnDelay);
            spawnProtectionDuration = Mathf.Max(0f, spawnProtectionDuration);
            minimumCharacterSeparation = Mathf.Max(0f, minimumCharacterSeparation);
            randomSpawnAttempts = Mathf.Max(1, randomSpawnAttempts);
            shieldRadius = Mathf.Max(0.1f, shieldRadius);
            shieldLineWidth = Mathf.Max(0.01f, shieldLineWidth);
            shieldSegments = Mathf.Clamp(shieldSegments, 12, 96);
            ResolveReferences();
        }

        private void OnDestroy()
        {
            if (generatedShield != null)
            {
                Destroy(generatedShield);
            }

            if (generatedShieldMaterial != null)
            {
                Destroy(generatedShieldMaterial);
            }
        }

        public void BeginRespawn()
        {
            if (respawnRoutine != null)
            {
                return;
            }

            ResolveReferences();
            if (characterNumber == null)
            {
                return;
            }

            respawnRoutine = StartCoroutine(RespawnSequence());
        }

        private IEnumerator RespawnSequence()
        {
            HideAndSuspendCharacter();

            if (respawnDelay > 0f)
            {
                yield return new WaitForSeconds(respawnDelay);
            }

            MoveToRandomArenaCell();
            PrepareRandomRespawnWeapon();
            characterNumber.ResetValue();
            RestoreCharacter();

            if (spawnProtectionDuration > 0f)
            {
                characterNumber.SetDamageImmunity(true);
                SetShieldVisible(true);
                yield return new WaitForSeconds(spawnProtectionDuration);
                characterNumber.SetDamageImmunity(false);
                SetShieldVisible(false);
            }

            respawnRoutine = null;
        }

        private void HideAndSuspendCharacter()
        {
            characterNumber.SetDamageImmunity(false);

            if (weaponHolder != null && weaponHolder.EquippedWeapon != null)
            {
                assignedWeapon = weaponHolder.EquippedWeapon;
            }

            suspendedBehaviours.Clear();
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (!ShouldSuspend(behaviour))
                {
                    continue;
                }

                if (behaviour is MeleeWeaponController weapon)
                {
                    weapon.CancelCurrentAction();
                }

                suspendedBehaviours.Add(new BehaviourState
                {
                    Behaviour = behaviour,
                    WasEnabled = behaviour.enabled
                });
                behaviour.enabled = false;
            }

            hiddenRenderers.Clear();
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in renderers)
            {
                hiddenRenderers.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    WasEnabled = targetRenderer.enabled
                });
                targetRenderer.enabled = false;
            }

            disabledColliders.Clear();
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D targetCollider in colliders)
            {
                disabledColliders.Add(new ColliderState
                {
                    Collider = targetCollider,
                    WasEnabled = targetCollider.enabled
                });
                targetCollider.enabled = false;
            }

            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                bodyWasSimulated = body.simulated;
                body.simulated = false;
            }
        }

        private void RestoreCharacter()
        {
            foreach (RendererState state in hiddenRenderers)
            {
                if (state.Renderer != null)
                {
                    state.Renderer.enabled = state.WasEnabled;
                }
            }

            foreach (ColliderState state in disabledColliders)
            {
                if (state.Collider != null)
                {
                    state.Collider.enabled = state.WasEnabled;
                }
            }

            if (body != null)
            {
                body.simulated = bodyWasSimulated;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            foreach (BehaviourState state in suspendedBehaviours)
            {
                if (state.Behaviour != null)
                {
                    state.Behaviour.enabled = state.WasEnabled;
                }
            }

            hiddenRenderers.Clear();
            disabledColliders.Clear();
            suspendedBehaviours.Clear();
        }

        private void MoveToRandomArenaCell()
        {
            if (arena == null)
            {
                arena = FindObjectOfType<ArenaGrid>();
            }

            if (arena == null)
            {
                return;
            }

            Vector2 selectedPosition = GetRandomCellCenter();
            for (int attempt = 0; attempt < randomSpawnAttempts; attempt++)
            {
                Vector2 candidate = GetRandomCellCenter();
                selectedPosition = candidate;
                if (IsClearOfLivingCharacters(candidate))
                {
                    break;
                }
            }

            if (body != null)
            {
                body.position = selectedPosition;
            }
            else
            {
                Vector3 currentPosition = transform.position;
                transform.position = new Vector3(selectedPosition.x, selectedPosition.y, currentPosition.z);
            }
        }

        private Vector2 GetRandomCellCenter()
        {
            int column = Random.Range(0, arena.Columns);
            int row = Random.Range(0, arena.Rows);
            float localX = (column + 0.5f - arena.Columns * 0.5f) * arena.CellSize;
            float localY = (row + 0.5f - arena.Rows * 0.5f) * arena.CellSize;
            return arena.transform.TransformPoint(new Vector3(localX, localY, 0f));
        }

        private bool IsClearOfLivingCharacters(Vector2 candidate)
        {
            if (arena != null && arena.OverlapsObstacle(candidate, Vector2.one * 0.9f))
            {
                return false;
            }

            float minimumDistanceSquared = minimumCharacterSeparation * minimumCharacterSeparation;
            CharacterNumber[] characters = FindObjectsOfType<CharacterNumber>();
            foreach (CharacterNumber otherCharacter in characters)
            {
                if (otherCharacter == null
                    || otherCharacter == characterNumber
                    || otherCharacter.IsEliminated
                    || !otherCharacter.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 offset = (Vector2)otherCharacter.transform.position - candidate;
                if (offset.sqrMagnitude < minimumDistanceSquared)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldSuspend(MonoBehaviour behaviour)
        {
            return behaviour is PlayerMovementController
                || behaviour is PlayerSkillStateMachine
                || behaviour is PlayerExecutionSkill
                || behaviour is PlayerProtectionSkill
                || behaviour is EnemyController
                || behaviour is MeleeWeaponController;
        }

        private void ResolveReferences()
        {
            if (characterNumber == null)
            {
                characterNumber = GetComponent<CharacterNumber>();
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (arena == null && Application.isPlaying)
            {
                arena = FindObjectOfType<ArenaGrid>();
            }

            if (weaponHolder == null)
            {
                weaponHolder = GetComponent<WeaponHolder>();
            }

            if (assignedWeapon == null && weaponHolder != null)
            {
                assignedWeapon = weaponHolder.EquippedWeapon;
                if (assignedWeapon == null)
                {
                    assignedWeapon = GetComponentInChildren<MeleeWeaponController>(true);
                }
            }
        }

        private void PrepareRandomRespawnWeapon()
        {
            if (weaponHolder == null)
            {
                weaponHolder = GetComponent<WeaponHolder>();
            }

            if (weaponHolder == null)
            {
                return;
            }

            MeleeWeaponController respawnWeapon = weaponHolder.EquippedWeapon;
            if (respawnWeapon == null
                && assignedWeapon != null
                && assignedWeapon.CurrentHolder == null
                && assignedWeapon.TryReturnToHolder(weaponHolder))
            {
                respawnWeapon = assignedWeapon;
            }

            if (respawnWeapon == null)
            {
                respawnWeapon = CreateReplacementWeapon();
            }

            if (respawnWeapon == null)
            {
                return;
            }

            assignedWeapon = respawnWeapon;
            respawnWeapon.RandomizeOperatorForHolder();
        }

        private MeleeWeaponController CreateReplacementWeapon()
        {
            GameObject weaponObject = new GameObject("Random Weapon");
            weaponObject.transform.SetParent(transform, false);
            weaponObject.transform.localPosition = Vector3.zero;
            weaponObject.transform.localRotation = Quaternion.identity;
            weaponObject.transform.localScale = Vector3.one;

            BoxCollider2D sensor = weaponObject.AddComponent<BoxCollider2D>();
            sensor.isTrigger = true;
            sensor.offset = new Vector2(0.9f, 0f);
            sensor.size = new Vector2(2f, 0.5f);

            MeleeWeaponController weapon = weaponObject.AddComponent<MeleeWeaponController>();
            weapon.SetAcceptPlayerInput(GetComponent<PlayerMovementController>() != null);
            weapon.SetRandomizeOperatorOnStart(false);
            weaponObject.AddComponent<OperatorWeaponBodyView>();
            weaponObject.AddComponent<OperatorWeaponSymbolView>();
            return weapon.CurrentHolder == weaponHolder ? weapon : null;
        }

        private void SetShieldVisible(bool visible)
        {
            if (visible && generatedShield == null)
            {
                CreateShieldVisual();
            }

            if (generatedShield != null)
            {
                generatedShield.SetActive(visible);
            }
        }

        private void CreateShieldVisual()
        {
            generatedShield = new GameObject("Respawn Protection Circle (Generated)")
            {
                hideFlags = HideFlags.DontSave
            };
            generatedShield.transform.SetParent(transform, false);
            generatedShield.transform.localPosition = new Vector3(0f, 0f, -0.12f);
            generatedShield.transform.localRotation = Quaternion.identity;
            generatedShield.transform.localScale = Vector3.one;

            LineRenderer circle = generatedShield.AddComponent<LineRenderer>();
            circle.useWorldSpace = false;
            circle.loop = true;
            circle.positionCount = shieldSegments;
            circle.startWidth = shieldLineWidth;
            circle.endWidth = shieldLineWidth;
            circle.startColor = shieldColor;
            circle.endColor = shieldColor;
            circle.numCornerVertices = 4;
            circle.numCapVertices = 4;
            circle.sortingOrder = 25;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            generatedShieldMaterial = new Material(shader)
            {
                name = "Respawn Protection Circle Material (Generated)",
                hideFlags = HideFlags.DontSave,
                color = shieldColor
            };
            circle.sharedMaterial = generatedShieldMaterial;

            for (int segment = 0; segment < shieldSegments; segment++)
            {
                float angle = Mathf.PI * 2f * segment / shieldSegments;
                circle.SetPosition(
                    segment,
                    new Vector3(Mathf.Cos(angle) * shieldRadius, Mathf.Sin(angle) * shieldRadius, 0f));
            }
        }
    }
}
