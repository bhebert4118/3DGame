using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on the guard-room computer in the 3D scene. Call <see cref="Interact"/>
/// from whatever interaction system you use (button press, trigger, UnityEvent...).
/// Opens the alarm puzzle and raises <see cref="onSolved"/> when the player solves it.
/// </summary>
public class AlarmComputerInteractable : MonoBehaviour
{
    [Tooltip("Hook up the alarm / guard reaction here (or subscribe in code).")]
    public UnityEvent onSolved;

    private bool _solved;

    public void Interact()
    {
        if (_solved) return; // already triggered the alarm
        AlarmPuzzleLauncher.Open(onSolved: () =>
        {
            _solved = true;
            onSolved?.Invoke();
        });
    }
}
