using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonPlayerController : MonoBehaviour
{
    [SerializeField] private Transform view;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float mouseSensitivity = 1.5f;
    [SerializeField] private float gravity = -20f;
    private CharacterController characterController;
    private float pitch;
    private float verticalSpeed;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterController.detectCollisions = true;
        characterController.enableOverlapRecovery = true;
        characterController.height = 1.8f;
        characterController.radius = 0.4f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        if (view == null && Camera.main != null) view = Camera.main.transform;
        SetGameplayCursor();
    }

    private void Update()
    {
        if (view == null || Keyboard.current == null || Mouse.current == null) return;
        if (AlarmPuzzleLauncher.IsOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }
        SetGameplayCursor();
        Vector2 look = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up, look.x);
        pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 move = transform.right * Axis(Keyboard.current.dKey, Keyboard.current.aKey)
                     + transform.forward * Axis(Keyboard.current.wKey, Keyboard.current.sKey);
        move = Vector3.ClampMagnitude(move, 1f) * moveSpeed;
        if (characterController.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;
        move.y = verticalSpeed;
        characterController.Move(move * Time.deltaTime);
    }

    private static void SetGameplayCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static float Axis(UnityEngine.InputSystem.Controls.KeyControl positive, UnityEngine.InputSystem.Controls.KeyControl negative)
        => (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
}
