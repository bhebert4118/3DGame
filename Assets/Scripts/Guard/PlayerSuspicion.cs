using UnityEngine;

/// <summary>
/// Decides when the player is doing something a guard must not see, and flags the player's
/// <see cref="GuardDetectable"/> for exactly that long. A guard who has line of sight while the flag
/// is up reports it (GuardController.AnyGuardSpotted), which restarts the level.
///
/// Right now "suspicious" means the alarm's wire puzzle is open. Other puzzles can add to it by
/// calling <see cref="MarkSuspicious"/>, for example from a UnityEvent.
/// </summary>
[RequireComponent(typeof(GuardDetectable))]
public class PlayerSuspicion : MonoBehaviour
{
    private GuardDetectable _detectable;
    private float _suspiciousUntil;

    public bool IsSuspicious => AlarmPuzzleLauncher.IsOpen || Time.time < _suspiciousUntil;

    /// <summary>Counts the player as suspicious for the next <paramref name="seconds"/> seconds.</summary>
    public void MarkSuspicious(float seconds) => _suspiciousUntil = Mathf.Max(_suspiciousUntil, Time.time + seconds);

    private void Awake() => _detectable = GetComponent<GuardDetectable>();

    private void Update()
    {
        bool suspicious = IsSuspicious;
        if (_detectable.IsSuspicious != suspicious) _detectable.SetSuspicious(suspicious);
    }
}
