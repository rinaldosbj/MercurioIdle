using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField]
    private Transform target;

    [Header("Orbit")]
    [SerializeField]
    private float distance = 5f;

    [SerializeField]
    private float minDistance = 2f;

    [SerializeField]
    private float maxDistance = 10f;

    [SerializeField]
    private float yaw = 0f;

    [SerializeField]
    private float pitch = 20f;

    [SerializeField]
    private float minPitch = -10f;

    [SerializeField]
    private float maxPitch = 80f;

    [Header("Input")]
    [SerializeField]
    private float rotationSpeed = 0.2f;

    [SerializeField]
    private float zoomSpeed = 2f;

    [Header("Smoothing")]
    [SerializeField]
    private float rotationSmooth = 15f;

    [SerializeField]
    private float zoomSmooth = 10f;

    private float targetDistance;
    private float targetYaw;
    private float targetPitch;

    private void Awake()
    {
        targetDistance = distance;
        targetYaw = yaw;
        targetPitch = pitch;
    }

    private void Update()
    {
        HandleMouse();
        HandleTouch();

        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        yaw = Mathf.LerpAngle(yaw, targetYaw, rotationSmooth * Time.deltaTime);

        pitch = Mathf.Lerp(pitch, targetPitch, rotationSmooth * Time.deltaTime);

        distance = Mathf.Lerp(distance, targetDistance, zoomSmooth * Time.deltaTime);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 position = target.position + rotation * Vector3.back * distance;

        transform.position = position;
        transform.rotation = rotation;
    }

    private void HandleMouse()
    {
        if (Mouse.current == null)
            return;

        // Rotacionar segurando botão esquerdo
        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();

            targetYaw += delta.x * rotationSpeed;
            targetPitch -= delta.y * rotationSpeed;
        }

        // Zoom pelo scroll
        float scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetDistance -= scroll * zoomSpeed * 0.01f;
        }
    }

    private void HandleTouch()
    {
        if (Touchscreen.current == null)
            return;

        var touch = Touchscreen.current.primaryTouch;

        if (!touch.press.isPressed)
            return;

        Vector2 delta = touch.delta.ReadValue();

        targetYaw += delta.x * rotationSpeed;
        targetPitch -= delta.y * rotationSpeed;
    }
}
