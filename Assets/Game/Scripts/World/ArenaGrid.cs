using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeCastle.World
{
    /// <summary>
    /// Draws a square arena where one cell equals one Unity world unit and
    /// configures four colliders that keep characters inside the arena.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ArenaGrid : MonoBehaviour
    {
        private const int BoundaryCount = 4;
        private const string IllustratedMapResourcePath = "ClassroomMap";

        // Coordinates are one-based (row, column), counted from the top-left
        // corner of the illustrated 10x10 grid.
        private static readonly Vector2Int[] ObstacleCoordinates =
        {
            new Vector2Int(1, 7),
            new Vector2Int(2, 2),
            new Vector2Int(4, 3),
            new Vector2Int(4, 9),
            new Vector2Int(6, 6),
            new Vector2Int(8, 3),
            new Vector2Int(9, 8)
        };

        [Header("Grid Size")]
        [SerializeField] private bool useIllustratedMap = true;
        [SerializeField, Min(1)] private int columns = 10;
        [SerializeField, Min(1)] private int rows = 10;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;

        [Header("Appearance")]
        [SerializeField, Min(0.005f)] private float lineWidth = 0.025f;
        [SerializeField] private Color backgroundColor = new Color(0.075f, 0.10f, 0.14f, 1f);
        [SerializeField] private Color gridColor = new Color(0.22f, 0.31f, 0.40f, 1f);
        [SerializeField] private Color borderColor = new Color(0.45f, 0.72f, 0.92f, 1f);
        [SerializeField] private Color obstacleColor = new Color(0.24f, 0.28f, 0.34f, 1f);
        [SerializeField] private Color obstacleBorderColor = new Color(0.72f, 0.80f, 0.88f, 1f);

        [Header("Collision")]
        [SerializeField, Min(0.1f)] private float boundaryThickness = 0.5f;
        [SerializeField, Range(0.1f, 1f)] private float obstacleColliderScale = 0.6f;

        private Mesh generatedMesh;
        private Material generatedMaterial;
        private GameObject generatedMapBackground;
        private SpriteRenderer mapBackgroundRenderer;

        public int Columns => columns;
        public int Rows => rows;
        public float CellSize => cellSize;
        public Vector2 Size => new Vector2(columns * cellSize, rows * cellSize);
        public int ObstacleCount => ObstacleCoordinates.Length;

        private void Awake()
        {
            ApplyIllustratedMapDimensions();
            RebuildArena();
        }

        private void OnEnable()
        {
            ApplyIllustratedMapDimensions();
            RebuildVisual();
        }

        private void OnValidate()
        {
            ApplyIllustratedMapDimensions();
            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);
            cellSize = Mathf.Max(0.01f, cellSize);
            lineWidth = Mathf.Clamp(lineWidth, 0.005f, cellSize * 0.25f);
            boundaryThickness = Mathf.Max(0.1f, boundaryThickness);
            obstacleColliderScale = Mathf.Clamp(obstacleColliderScale, 0.1f, 1f);

            RebuildVisual();

            if (GetComponents<BoxCollider2D>().Length >= BoundaryCount + ObstacleCoordinates.Length)
            {
                ConfigureArenaColliders();
            }
        }

        private void OnDestroy()
        {
            DestroyGeneratedObject(generatedMesh);
            DestroyGeneratedObject(generatedMaterial);
        }

        /// <summary>
        /// Rebuilds both the visual grid and the arena boundary. This is also
        /// used by the scene setup command in the Unity editor.
        /// </summary>
        public void RebuildArena()
        {
            ApplyIllustratedMapDimensions();
            RebuildVisual();
            EnsureArenaColliders();
            ConfigureArenaColliders();
        }

        private void RebuildVisual()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

            if (TryConfigureIllustratedBackground())
            {
                if (generatedMesh != null)
                {
                    generatedMesh.Clear();
                }

                meshFilter.sharedMesh = null;
                meshRenderer.enabled = false;
                return;
            }

            if (generatedMapBackground != null)
            {
                generatedMapBackground.SetActive(false);
            }

            meshRenderer.enabled = true;

            if (generatedMesh == null)
            {
                generatedMesh = new Mesh
                {
                    name = "Arena Grid (Generated)",
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

            float width = columns * cellSize;
            float height = rows * cellSize;
            float minX = -width * 0.5f;
            float maxX = width * 0.5f;
            float minY = -height * 0.5f;
            float maxY = height * 0.5f;

            AddQuad(vertices, triangles, colors, minX, minY, maxX, maxY, 0.1f, backgroundColor);

            for (int column = 0; column <= columns; column++)
            {
                float x = minX + column * cellSize;
                Color color = column == 0 || column == columns ? borderColor : gridColor;
                float widthForLine = column == 0 || column == columns ? lineWidth * 2f : lineWidth;
                AddQuad(vertices, triangles, colors, x - widthForLine * 0.5f, minY,
                    x + widthForLine * 0.5f, maxY, 0f, color);
            }

            for (int row = 0; row <= rows; row++)
            {
                float y = minY + row * cellSize;
                Color color = row == 0 || row == rows ? borderColor : gridColor;
                float widthForLine = row == 0 || row == rows ? lineWidth * 2f : lineWidth;
                AddQuad(vertices, triangles, colors, minX, y - widthForLine * 0.5f,
                    maxX, y + widthForLine * 0.5f, 0f, color);
            }

            foreach (Vector2Int coordinate in ObstacleCoordinates)
            {
                Vector2 obstacleCenter = GetObstacleLocalCenter(coordinate);
                float centerX = obstacleCenter.x;
                float centerY = obstacleCenter.y;
                float outerHalfSize = cellSize * 0.5f;
                float innerHalfSize = Mathf.Max(0f, outerHalfSize - lineWidth * 2f);

                AddQuad(vertices, triangles, colors,
                    centerX - outerHalfSize, centerY - outerHalfSize,
                    centerX + outerHalfSize, centerY + outerHalfSize,
                    -0.03f, obstacleBorderColor);
                AddQuad(vertices, triangles, colors,
                    centerX - innerHalfSize, centerY - innerHalfSize,
                    centerX + innerHalfSize, centerY + innerHalfSize,
                    -0.04f, obstacleColor);
            }

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
                    name = "Arena Grid Material (Generated)",
                    hideFlags = HideFlags.DontSave
                };
            }

            meshRenderer.sharedMaterial = generatedMaterial;
            meshRenderer.sortingOrder = -10;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        public bool OverlapsObstacle(Vector2 worldPosition, Vector2 worldSize)
        {
            Vector2 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector2 halfSize = worldSize * 0.5f;
            float obstacleHalfSize = cellSize * obstacleColliderScale * 0.5f;

            foreach (Vector2Int coordinate in ObstacleCoordinates)
            {
                Vector2 obstacleCenter = GetObstacleLocalCenter(coordinate);
                if (Mathf.Abs(localPosition.x - obstacleCenter.x) < halfSize.x + obstacleHalfSize
                    && Mathf.Abs(localPosition.y - obstacleCenter.y) < halfSize.y + obstacleHalfSize)
                {
                    return true;
                }
            }

            return false;
        }

        private void EnsureArenaColliders()
        {
            int existingCount = GetComponents<BoxCollider2D>().Length;
            int requiredCount = BoundaryCount + ObstacleCoordinates.Length;
            for (int index = existingCount; index < requiredCount; index++)
            {
                gameObject.AddComponent<BoxCollider2D>();
            }
        }

        private void ConfigureArenaColliders()
        {
            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            if (colliders.Length < BoundaryCount + ObstacleCoordinates.Length)
            {
                return;
            }

            float width = columns * cellSize;
            float height = rows * cellSize;
            float horizontalOffset = width * 0.5f + boundaryThickness * 0.5f;
            float verticalOffset = height * 0.5f + boundaryThickness * 0.5f;

            ConfigureCollider(colliders[0], new Vector2(-horizontalOffset, 0f),
                new Vector2(boundaryThickness, height + boundaryThickness * 2f));
            ConfigureCollider(colliders[1], new Vector2(horizontalOffset, 0f),
                new Vector2(boundaryThickness, height + boundaryThickness * 2f));
            ConfigureCollider(colliders[2], new Vector2(0f, -verticalOffset),
                new Vector2(width, boundaryThickness));
            ConfigureCollider(colliders[3], new Vector2(0f, verticalOffset),
                new Vector2(width, boundaryThickness));

            for (int index = 0; index < ObstacleCoordinates.Length; index++)
            {
                Vector2 obstacleCenter = GetObstacleLocalCenter(ObstacleCoordinates[index]);
                ConfigureCollider(
                    colliders[BoundaryCount + index],
                    obstacleCenter,
                    Vector2.one * cellSize * obstacleColliderScale);
            }
        }

        private static void ConfigureCollider(BoxCollider2D collider, Vector2 offset, Vector2 size)
        {
            collider.isTrigger = false;
            collider.offset = offset;
            collider.size = size;
        }

        private Vector2 GetObstacleLocalCenter(Vector2Int rowAndColumn)
        {
            int row = rowAndColumn.x;
            int column = rowAndColumn.y;
            float x = (column - 0.5f - columns * 0.5f) * cellSize;
            float y = (rows * 0.5f - row + 0.5f) * cellSize;
            return new Vector2(x, y);
        }

        private void ApplyIllustratedMapDimensions()
        {
            if (!useIllustratedMap)
            {
                return;
            }

            columns = 10;
            rows = 10;
            cellSize = 1f;
        }

        private bool TryConfigureIllustratedBackground()
        {
            if (!useIllustratedMap)
            {
                return false;
            }

            Sprite mapSprite = Resources.Load<Sprite>(IllustratedMapResourcePath);
            if (mapSprite == null)
            {
                return false;
            }

            if (generatedMapBackground == null)
            {
                Transform existingBackground = transform.Find("Classroom Map (Generated)");
                if (existingBackground != null)
                {
                    generatedMapBackground = existingBackground.gameObject;
                }
                else
                {
                    generatedMapBackground = new GameObject("Classroom Map (Generated)")
                    {
                        hideFlags = HideFlags.DontSave
                    };
                    generatedMapBackground.transform.SetParent(transform, false);
                }
            }

            if (mapBackgroundRenderer == null)
            {
                mapBackgroundRenderer = generatedMapBackground.GetComponent<SpriteRenderer>();
                if (mapBackgroundRenderer == null)
                {
                    mapBackgroundRenderer = generatedMapBackground.AddComponent<SpriteRenderer>();
                }
            }

            generatedMapBackground.SetActive(true);
            generatedMapBackground.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            generatedMapBackground.transform.localRotation = Quaternion.identity;
            generatedMapBackground.transform.localScale = Vector3.one;
            mapBackgroundRenderer.sprite = mapSprite;
            mapBackgroundRenderer.color = Color.white;
            mapBackgroundRenderer.sortingOrder = -20;
            return true;
        }

        private static void AddQuad(
            ICollection<Vector3> vertices,
            ICollection<int> triangles,
            ICollection<Color> colors,
            float minX,
            float minY,
            float maxX,
            float maxY,
            float z,
            Color color)
        {
            int startIndex = vertices.Count;
            vertices.Add(new Vector3(minX, minY, z));
            vertices.Add(new Vector3(minX, maxY, z));
            vertices.Add(new Vector3(maxX, maxY, z));
            vertices.Add(new Vector3(maxX, minY, z));

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
