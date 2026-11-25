using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on anything a guard should react to seeing (the player, the prisoner).
/// It only counts while <see cref="IsSuspicious"/> is true, so the player standing around
/// or the prisoner sitting in his cell doesn't get "caught". For the player, PlayerSuspicion
/// switches it on while he is tampering with something. Whoever moves the prisoner out of the
/// cell (cell door puzzle / escape sequence) should call SetSuspicious(true).
/// </summary>
public class GuardDetectable : MonoBehaviour
{
    [Tooltip("Only seen by the guard while this is on. Turn on when the prisoner leaves his cell.")]
    [SerializeField] private bool isSuspicious = false;

    [Tooltip("Point the guard looks at, relative to this object (roughly chest height).")]
    [SerializeField] private Vector3 aimOffset = new Vector3(0f, 1.2f, 0f);

    private static readonly List<GuardDetectable> _all = new List<GuardDetectable>();
    public static IReadOnlyList<GuardDetectable> All => _all;

    public bool IsSuspicious => isSuspicious;
    public Vector3 AimPoint => transform.TransformPoint(aimOffset);

    /// <summary>UnityEvent-friendly toggle.</summary>
    public void SetSuspicious(bool value) => isSuspicious = value;

    private void OnEnable() { if (!_all.Contains(this)) _all.Add(this); }
    private void OnDisable() => _all.Remove(this);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isSuspicious ? Color.red : Color.green;
        Gizmos.DrawWireSphere(AimPoint, 0.15f);
    }
}
