using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;

public class CameraMovement : MonoBehaviour
{
    private Entity selectedEntity = Entity.Null;
    private bool isDragging = false;
    private Camera cam;
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float fastMultiplier = 2f;
    public float slowMultiplier = 0.5f;

    [Header("Mouse Look Settings")]
    public float lookSensitivity = 2f;
    public bool enableMouseLook = false;

    private float rotationX;
    private float rotationY;

    [Header("Mouse Click Settings")]
    public float maxDistance = 100f;

    void Awake()
    {
        // Get the Camera component from the same GameObject this script is attached to
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        // Lock cursor for a better camera feel
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Initialize rotation
        rotationX = transform.localRotation.eulerAngles.y;
        rotationY = transform.localRotation.eulerAngles.x;
    }

    void Update()
    {
        CheckMove();
        if (!Cursor.visible)
        {
            HandleMovement();
            if (enableMouseLook)
                HandleMouseLook();
        }
        HandleClick();
    }

    void CheckMove()
    {
        if (Cursor.visible)
        {
            Cursor.lockState = CursorLockMode.None;
            enableMouseLook = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            enableMouseLook = true;
        }
    }

    void HandleMovement()
    {
        // Determine movement speed
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl))
            speed *= slowMultiplier;

        Vector3 direction = Vector3.zero;

        // WASD for horizontal movement
        if (Input.GetKey(KeyCode.W))
            direction += transform.forward;
        if (Input.GetKey(KeyCode.S))
            direction -= transform.forward;
        if (Input.GetKey(KeyCode.A))
            direction -= transform.right;
        if (Input.GetKey(KeyCode.D))
            direction += transform.right;

        // Optional up/down movement
        if (Input.GetKey(KeyCode.E))
            direction += transform.up;
        if (Input.GetKey(KeyCode.Q))
            direction -= transform.up;

        // Apply movement
        transform.position += direction.normalized * speed * Time.deltaTime;
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;

        rotationX += mouseX;
        rotationY -= mouseY;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f); // Prevent flipping

        transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0f);
    }

    void HandleClick()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;
        var physicsWorldSingletonQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
        var physicsWorldSingleton = physicsWorldSingletonQuery.GetSingleton<PhysicsWorldSingleton>();
        var collisionWorld = physicsWorldSingleton.CollisionWorld;
        var physicsWorld   = physicsWorldSingleton.PhysicsWorld;
        // Right-click toggles mouse visibility mode
        if (Mouse.current.rightButton.wasPressedThisFrame)
            Cursor.visible = !Cursor.visible;

        if (Mouse.current.leftButton.wasPressedThisFrame && !Cursor.visible)
        {
            // Ray from the center of this camera’s viewport
            UnityEngine.Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var rayInput = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * maxDistance,
                Filter = CollisionFilter.Default
            };
            if (collisionWorld.CastRay(rayInput, out Unity.Physics.RaycastHit hit))
            {
                selectedEntity = physicsWorld.Bodies[hit.RigidBodyIndex].Entity;
                isDragging = true;
                Debug.Log($"Selected entity: {selectedEntity.Index}");
            }
            else
            {
                selectedEntity = Entity.Null;
                isDragging = false;
                Debug.Log("No entity hit");
            }
        }
        // While holding left mouse, move selected entity
        if (Mouse.current.leftButton.isPressed && isDragging && selectedEntity != Entity.Null)
        {
            Debug.Log("Dragging");
            var p = MouseWorld.RayToPlane(Input.mousePosition, cam, new float3(0, 0, 0), new float3(0, 1, 0));

            var queue = em.CreateEntityQuery(typeof(EditQueueTag)).GetSingletonEntity();
            var buf = em.GetBuffer<EditRequest>(queue);
            buf.Add(new EditRequest
            {
                Op = EditOp.Move,
                Target = selectedEntity,
                P = p
            });
        }

        // When released, stop dragging
        if (Mouse.current.leftButton.wasReleasedThisFrame)
            isDragging = false;
    }
}

public static class MouseWorld
{
    public static float3 RayToPlane(Vector2 mousePos, Camera cam, float3 planeOrigin, float3 planeNormal)
    {
        UnityEngine.Ray ray = cam.ScreenPointToRay(mousePos);
        var n = (Vector3)planeNormal;
        float denom = Vector3.Dot(n, ray.direction);
        if (math.abs(denom) < 1e-6f) return planeOrigin;
        float t = Vector3.Dot((Vector3)planeOrigin - ray.origin, n) / denom;
        return (float3)(ray.origin + ray.direction * t);
    }
}
