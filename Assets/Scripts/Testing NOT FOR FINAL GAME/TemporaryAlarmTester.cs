using UnityEngine;
using UnityEngine.InputSystem;

public class TemporaryAlarmTester : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float interactionDistance = 5f;

    private void Awake()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;
    }

    private void Update()
    {
        if (Keyboard.current == null ||
            !Keyboard.current.eKey.wasPressedThisFrame ||
            viewCamera == null)
        {
            return;
        }

        Ray ray = viewCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            AlarmComputerInteractable alarm =
                hit.collider.GetComponentInParent<AlarmComputerInteractable>();

            if (alarm != null)
            {
                alarm.Interact();
            }
        }
    }
}