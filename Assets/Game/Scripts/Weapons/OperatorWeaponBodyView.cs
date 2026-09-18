using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeCastle.Weapons
{
    /// <summary>
    /// Temporary rectangular weapon body whose outer dimensions exactly match
    /// the configured sensor. Replace this view when final sprites arrive.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeWeaponController))]
    public sealed class OperatorWeaponBodyView : MonoBehaviour
    {
        private const string BodyName = "Operator Body (Generated)";

        [SerializeField] private Color weaponColor = new Color(1f, 0.24f, 0.12f, 1f);
        [SerializeField] private Color outlineColor = new Color(0.28f, 0.035f, 0.02f, 1f);
        [SerializeField] private int sortingOrder = 8;

        private GameObject generatedBody;
        private Mesh generatedMesh;
        private Material generatedMaterial;
        private MeleeWeaponController weaponController;

        private void OnEnable()
        {
            Rebuild();
        }

        private void Update()
        {
            if (generatedMaterial == null)
            {
                return;
            }

            float charge = weaponController != null ? weaponController.Charge01 : 0f;
            generatedMaterial.color = Color.Lerp(
                Color.white,
                new Color(1.35f, 1.35f, 0.55f, 1f),
                charge);
        }

        private void OnValidate()
        {
            Rebuild();
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(generatedBody);
            DestroyGeneratedObject(generatedMesh);
            DestroyGeneratedObject(generatedMaterial);
        }

        public void Rebuild()
        {
            weaponController = GetComponent<MeleeWeaponController>();
            if (weaponController == null)
            {
                return;
            }

            Transform existingBody = transform.Find(BodyName);
            if (existingBody != null)
            {
                generatedBody = existingBody.gameObject;
            }

            if (generatedBody == null)
            {
                generatedBody = new GameObject(BodyName)
                {
                    hideFlags = HideFlags.DontSave
                };
                generatedBody.transform.SetParent(transform, false);
            }

            generatedBody.transform.localPosition = Vector3.zero;
            generatedBody.transform.localRotation = Quaternion.identity;
            generatedBody.transform.localScale = Vector3.one;

            MeshFilter meshFilter = generatedBody.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = generatedBody.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = generatedBody.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = generatedBody.AddComponent<MeshRenderer>();
            }

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "Operator Weapon Body (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }
            else
            {
                generatedMesh.Clear();
            }

            Vector2 outerSize = weaponController.SensorSize;
            Vector2 innerSize = new Vector2(
                Mathf.Max(0.01f, outerSize.x - 0.08f),
                Mathf.Max(0.01f, outerSize.y - 0.08f));
            float centerX = weaponController.WeaponCenterDistance;

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();
            AddQuad(vertices, triangles, colors, centerX, outerSize, 0.01f, outlineColor);
            AddQuad(vertices, triangles, colors, centerX, innerSize, 0f, weaponColor);

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
                    name = "Operator Weapon Material (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.sharedMaterial = generatedMaterial;
            meshRenderer.sortingOrder = sortingOrder;
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
            Vector2 size,
            float z,
            Color color)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
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
