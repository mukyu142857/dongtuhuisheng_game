using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeCastle.Weapons
{
    /// <summary>
    /// Temporary minus-sign artwork generated without external image assets.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeleeWeaponController))]
    public sealed class MinusWeaponView : MonoBehaviour
    {
        [SerializeField] private Color weaponColor = new Color(1f, 0.24f, 0.12f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.28f, 0.035f, 0.02f, 1f);
        [SerializeField, Range(0.05f, 0.5f)] private float barHeight = 0.22f;

        private Mesh generatedMesh;
        private Material generatedMaterial;
        private MeleeWeaponController weaponController;

        private void OnEnable()
        {
            weaponController = GetComponent<MeleeWeaponController>();
            Rebuild();
        }

        private void Update()
        {
            if (generatedMaterial == null)
            {
                return;
            }

            float charge = weaponController != null ? weaponController.Charge01 : 0f;
            generatedMaterial.color = Color.Lerp(Color.white, new Color(1.35f, 1.35f, 0.55f, 1f), charge);
        }

        private void OnValidate()
        {
            barHeight = Mathf.Clamp(barHeight, 0.05f, 0.5f);
            Rebuild();
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(generatedMesh);
            DestroyGeneratedObject(generatedMaterial);
        }

        private void Rebuild()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            weaponController = GetComponent<MeleeWeaponController>();
            float centerX = weaponController != null ? weaponController.WeaponCenterDistance : 0.9f;

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "Minus Weapon (Generated)",
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

            AddQuad(vertices, triangles, colors, centerX, 1.08f, barHeight + 0.1f, 0.01f, outlineColor);
            AddQuad(vertices, triangles, colors, centerX, 1f, barHeight, 0f, weaponColor);

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
                    name = "Minus Weapon Material (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.sharedMaterial = generatedMaterial;
            meshRenderer.sortingOrder = 20;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void AddQuad(
            ICollection<Vector3> vertices,
            ICollection<int> triangles,
            ICollection<Color> colors,
            float centerX,
            float width,
            float height,
            float z,
            Color color)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            int startIndex = vertices.Count;

            vertices.Add(new Vector3(centerX - halfWidth, -halfHeight, z));
            vertices.Add(new Vector3(centerX - halfWidth, halfHeight, z));
            vertices.Add(new Vector3(centerX + halfWidth, halfHeight, z));
            vertices.Add(new Vector3(centerX + halfWidth, -halfHeight, z));

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
