using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeCastle.Characters
{
    /// <summary>
    /// Temporary programmer art for the player. It deliberately keeps the
    /// visible square slightly smaller than the exact 1x1 physics hitbox.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlayerAvatarView : MonoBehaviour
    {
        private const string LabelName = "Number Label (Generated)";

        [SerializeField] private string numberText = "1";
        [SerializeField] private Color bodyColor = new Color(0.12f, 0.68f, 0.95f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.02f, 0.12f, 0.20f, 1f);
        [SerializeField, Range(0.5f, 1f)] private float visualSize = 0.9f;

        private Mesh generatedMesh;
        private Material generatedMaterial;
        private GameObject generatedLabel;

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnValidate()
        {
            visualSize = Mathf.Clamp(visualSize, 0.5f, 1f);
            if (string.IsNullOrWhiteSpace(numberText))
            {
                numberText = "1";
            }

            Rebuild();
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(generatedLabel);
            DestroyGeneratedObject(generatedMesh);
            DestroyGeneratedObject(generatedMaterial);
        }

        private void Rebuild()
        {
            BuildBodyMesh();
            BuildNumberLabel();
        }

        public void Configure(string displayedNumber, Color newBodyColor)
        {
            numberText = string.IsNullOrWhiteSpace(displayedNumber) ? "1" : displayedNumber;
            bodyColor = newBodyColor;
            Rebuild();
        }

        public void SetNumberText(string displayedNumber)
        {
            numberText = string.IsNullOrWhiteSpace(displayedNumber) ? "0" : displayedNumber;
            BuildNumberLabel();
        }

        private void BuildBodyMesh()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "Player Avatar (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                generatedMesh.Clear();
            }

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();
            float outerHalfSize = visualSize * 0.5f;
            float innerHalfSize = Mathf.Max(0f, outerHalfSize - 0.055f);

            AddQuad(vertices, triangles, colors, outerHalfSize, 0.01f, outlineColor);
            AddQuad(vertices, triangles, colors, innerHalfSize, 0f, bodyColor);

            generatedMesh.SetVertices(vertices);
            generatedMesh.SetTriangles(triangles, 0);
            generatedMesh.SetColors(colors);
            generatedMesh.RecalculateBounds();
            meshFilter.sharedMesh = generatedMesh;

            if (generatedMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }

                generatedMaterial = new Material(shader)
                {
                    name = "Player Avatar Material (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.sharedMaterial = generatedMaterial;
            meshRenderer.sortingOrder = 10;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void BuildNumberLabel()
        {
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

            generatedLabel.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            generatedLabel.transform.localRotation = Quaternion.identity;
            generatedLabel.transform.localScale = Vector3.one;

            TextMesh label = generatedLabel.GetComponent<TextMesh>();
            if (label == null)
            {
                label = generatedLabel.AddComponent<TextMesh>();
            }

            label.text = numberText;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.1f;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;

            MeshRenderer labelRenderer = generatedLabel.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 11;
            }
        }

        private static void AddQuad(
            ICollection<Vector3> vertices,
            ICollection<int> triangles,
            ICollection<Color> colors,
            float halfSize,
            float z,
            Color color)
        {
            int startIndex = vertices.Count;
            vertices.Add(new Vector3(-halfSize, -halfSize, z));
            vertices.Add(new Vector3(-halfSize, halfSize, z));
            vertices.Add(new Vector3(halfSize, halfSize, z));
            vertices.Add(new Vector3(halfSize, -halfSize, z));

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);

            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
