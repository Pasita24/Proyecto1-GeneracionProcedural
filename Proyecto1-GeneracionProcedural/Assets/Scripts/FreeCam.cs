using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCam : MonoBehaviour
{
    public float moveSpeed = 20f;
    public float lookSpeed = 0.05f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        // Leer la tecla Escape con el nuevo sistema
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool isLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = isLocked;
        }

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // Leer el movimiento del ratón
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            rotationX -= mouseDelta.y * lookSpeed;
            rotationY += mouseDelta.x * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);

            float moveX = 0f;
            float moveZ = 0f;
            float moveY = 0f;

            // Leer las teclas WASD y Q/E
            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;
            if (Keyboard.current.eKey.isPressed) moveY = 1f;
            if (Keyboard.current.qKey.isPressed) moveY = -1f;

            Vector3 move = transform.right * moveX + transform.up * moveY + transform.forward * moveZ;
            transform.position += move * moveSpeed * Time.deltaTime;
        }
    }
}