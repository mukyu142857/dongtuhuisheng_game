using UnityEngine;

namespace ShapeCastle.Characters
{
    public enum ProtectionSkillState
    {
        Ready,
        Protecting,
        Cooldown
    }

    /// <summary>
    /// Q skill: temporarily prevents changes caused by incoming damage. Its
    /// eight-second personal cooldown begins only after protection expires.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerSkillStateMachine), typeof(CharacterNumber))]
    public sealed class PlayerProtectionSkill : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private KeyCode activationKey = KeyCode.Q;

        [Header("Timing")]
        [SerializeField, Min(0.01f)] private float protectionDuration = 3f;
        [SerializeField, Min(0f)] private float cooldownDuration = 8f;

        [Header("Shield Visual")]
        [SerializeField] private Color shieldColor = Color.white;
        [SerializeField, Min(0.1f)] private float shieldRadius = 0.7f;
        [SerializeField, Min(0.01f)] private float shieldLineWidth = 0.06f;
        [SerializeField, Range(12, 96)] private int shieldSegments = 48;

        [Header("References")]
        [SerializeField] private PlayerSkillStateMachine skillStateMachine;
        [SerializeField] private CharacterNumber characterNumber;

        [Header("Runtime (Read Only)")]
        [SerializeField] private ProtectionSkillState currentState = ProtectionSkillState.Ready;

        private PlayerSkillAction protectionAction;
        private float stateEndTime;
        private GameObject generatedShield;
        private Material generatedShieldMaterial;

        public ProtectionSkillState CurrentState => currentState;
        public bool IsProtecting => currentState == ProtectionSkillState.Protecting;
        public float RemainingTime => currentState == ProtectionSkillState.Ready
            ? 0f
            : Mathf.Max(0f, stateEndTime - Time.time);

        private void Awake()
        {
            ResolveReferences();
            protectionAction = BeginProtection;
            SetShieldVisible(false);
        }

        private void OnValidate()
        {
            protectionDuration = Mathf.Max(0.01f, protectionDuration);
            cooldownDuration = Mathf.Max(0f, cooldownDuration);
            shieldRadius = Mathf.Max(0.1f, shieldRadius);
            shieldLineWidth = Mathf.Max(0.01f, shieldLineWidth);
            shieldSegments = Mathf.Clamp(shieldSegments, 12, 96);
            ResolveReferences();
        }

        private void Update()
        {
            UpdateTimedState();

            if (Input.GetKeyDown(activationKey))
            {
                TryActivate();
            }
        }

        private void OnDisable()
        {
            if (characterNumber != null)
            {
                characterNumber.SetDamageImmunity(false);
            }

            SetShieldVisible(false);
            currentState = ProtectionSkillState.Ready;
            stateEndTime = 0f;
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

        public bool TryActivate()
        {
            if (skillStateMachine == null || characterNumber == null)
            {
                ResolveReferences();
            }

            if (skillStateMachine == null || characterNumber == null || protectionAction == null)
            {
                return false;
            }

            if (!skillStateMachine.TryStartSkill(protectionAction))
            {
                return false;
            }

            skillStateMachine.CompleteCurrentSkill();
            return true;
        }

        private bool BeginProtection()
        {
            if (currentState != ProtectionSkillState.Ready
                || characterNumber.IsEliminated
                || characterNumber.IsDamageImmune)
            {
                return false;
            }

            characterNumber.SetDamageImmunity(true);
            SetShieldVisible(true);
            currentState = ProtectionSkillState.Protecting;
            stateEndTime = Time.time + protectionDuration;
            return true;
        }

        private void UpdateTimedState()
        {
            if (currentState == ProtectionSkillState.Protecting && Time.time >= stateEndTime)
            {
                characterNumber?.SetDamageImmunity(false);
                SetShieldVisible(false);
                currentState = cooldownDuration > 0f
                    ? ProtectionSkillState.Cooldown
                    : ProtectionSkillState.Ready;
                stateEndTime = Time.time + cooldownDuration;
            }
            else if (currentState == ProtectionSkillState.Cooldown && Time.time >= stateEndTime)
            {
                currentState = ProtectionSkillState.Ready;
                stateEndTime = 0f;
            }
        }

        private void ResolveReferences()
        {
            if (skillStateMachine == null)
            {
                skillStateMachine = GetComponent<PlayerSkillStateMachine>();
            }

            if (characterNumber == null)
            {
                characterNumber = GetComponent<CharacterNumber>();
            }
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
            generatedShield = new GameObject("Protection Circle (Generated)")
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
                name = "Protection Circle Material (Generated)",
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
