using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
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
        HandleMovement();
        if (enableMouseLook)
            HandleMouseLook();
        HandleClick();
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
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Ray from the center of this camera’s viewport
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                Debug.Log($"Hit: {hit.collider.gameObject.name}");
            }
            else
            {
                Debug.Log("No object hit.");
            }
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            enableMouseLook = !enableMouseLook;
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
    }
}
