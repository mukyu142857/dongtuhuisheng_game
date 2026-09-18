using UnityEngine;

namespace ShapeCastle.Weapons
{
    /// <summary>
    /// Temporary text-only identifier for arithmetic weapons. It can be
    /// removed when final weapon sprites are available.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeWeaponController))]
    public sealed class OperatorWeaponSymbolView : MonoBehaviour
    {
        private const string LabelName = "Operator Symbol (Generated)";

        [SerializeField] private Color symbolColor = Color.white;
        [SerializeField, Range(0.02f, 0.2f)] private float characterSize = 0.08f;
        [SerializeField] private int sortingOrder = 9;

        private GameObject generatedLabel;

        private void OnEnable()
        {
            Refresh();
        }

        private void OnValidate()
        {
            characterSize = Mathf.Clamp(characterSize, 0.02f, 0.2f);
            Refresh();
        }

        private void OnDestroy()
        {
            if (generatedLabel == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedLabel);
            }
            else
            {
                DestroyImmediate(generatedLabel);
            }
        }

        public void Refresh()
        {
            MeleeWeaponController controller = GetComponent<MeleeWeaponController>();
            if (controller == null)
            {
                return;
            }

            Transform existingLabel = transform.Find(LabelName);
            if (existingLabel != null)
            {
                generatedLabel = existingLabel.gameObject;
            }

            if (generatedLabel == null)
            {
                generatedLabel = new GameObject(LabelName)
                {
                    hideFlags = HideFlags.DontSave
                };
                generatedLabel.transform.SetParent(transform, false);
            }

            generatedLabel.transform.localPosition =
                new Vector3(controller.WeaponCenterDistance, 0f, -0.08f);
            generatedLabel.transform.localRotation = Quaternion.identity;
            generatedLabel.transform.localScale = Vector3.one;

            TextMesh label = generatedLabel.GetComponent<TextMesh>();
            if (label == null)
            {
                label = generatedLabel.AddComponent<TextMesh>();
            }

            label.text = GetSymbol(controller.OperatorType);
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.fontStyle = FontStyle.Bold;
            label.color = symbolColor;

            MeshRenderer labelRenderer = generatedLabel.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = sortingOrder;
            }
        }

        private static string GetSymbol(ArithmeticOperatorType operatorType)
        {
            switch (operatorType)
            {
                case ArithmeticOperatorType.Add:
                    return "+";
                case ArithmeticOperatorType.Multiply:
                    return "×";
                case ArithmeticOperatorType.Divide:
                    return "÷";
                default:
                    return "-";
            }
        }
    }
}
