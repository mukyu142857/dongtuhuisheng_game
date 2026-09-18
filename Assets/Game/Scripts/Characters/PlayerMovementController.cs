using UnityEngine;

namespace ShapeCastle.Characters
{
    /// <summary>
    /// Reads WASD and moves the player along the world X/Y axes.
    /// Physics movement is kept in FixedUpdate so arena collisions stay stable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerMovementController : MonoBehaviour, IMovementSpeedProvider
    {
        [SerializeField, Min(0f)] private float moveSpeed = 1f;

        private Rigidbody2D body;
        private Vector2 moveInput;

        public Vector2 WorldPosition => body != null ? body.position : (Vector2)transform.position;
        public float MoveSpeed => moveSpeed;

        private void Awake()
        {
            CacheAndConfigureComponents();
        }

        private void Reset()
        {
            CacheAndConfigureComponents();
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            CacheAndConfigureComponents();
        }

        private void Update()
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
            if (Input.GetKey(KeyCode.D)) horizontal += 1f;
            if (Input.GetKey(KeyCode.S)) vertical -= 1f;
            if (Input.GetKey(KeyCode.W)) vertical += 1f;

            moveInput = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
        }

        private void FixedUpdate()
        {
            body.velocity = moveInput * moveSpeed;
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            if (body != null)
            {
                body.velocity = Vector2.zero;
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
    }
}
