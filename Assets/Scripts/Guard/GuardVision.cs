using System;
using UnityEngine;

/// <summary>
/// Simple view cone for the guard. Every few frames it checks each suspicious
/// <see cref="GuardDetectable"/>: within range, inside the cone, and not blocked by a wall.
/// On a hit it tells the GuardController, which stops and raises onPrisonerSpotted.
/// Vision is off while the guard is away from his post (in the distraction room).
/// </summary>
[RequireComponent(typeof(GuardController))]
public class GuardVision : MonoBehaviour
{
    [SerializeField] private float viewDistance = 10f;
    [Tooltip("Full width of the view cone in degrees.")]
    [Range(10f, 180f)]
    [SerializeField] private float viewAngle = 80f;
    [SerializeField] private float eyeHeight = 1.6f;
    [Tooltip("What blocks line of sight. Default: everything.")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float checkInterval = 0.1f;

    /// <summary>C# event for code that wants to know who was seen.</summary>
    public event Action<GuardDetectable> TargetSpotted;

    private GuardController _guard;
    private float _nextCheck;

    private void Awake() => _guard = GetComponent<GuardController>();

    private void Update()
    {
        if (_guard.IsAwayFromPost || _guard.State == GuardState.Alert) return;
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + checkInterval;

        var targets = GuardDetectable.All;
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target == null || !target.IsSuspicious) continue;
            if (!CanSee(target)) continue;

            TargetSpotted?.Invoke(target);
            _guard.ReportSpotted(target.transform);
            return;
        }
    }

    public bool CanSee(GuardDetectable target)
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 to = target.AimPoint - eye;
        float distance = to.magnitude;
        if (distance > viewDistance) return false;
        if (Vector3.Angle(transform.forward, to) > viewAngle * 0.5f) return false;

        // Anything hit before the target that isn't part of the target blocks the view.
        // (A ray starting inside the guard's own collider doesn't hit it.)
        if (Physics.Raycast(eye, to / distance, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(target.transform) && !target.transform.IsChildOf(hit.transform)) return false;
        }
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        Vector3 left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward * viewDistance;
        Vector3 right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward * viewDistance;
        Gizmos.DrawLine(eye, eye + left);
        Gizmos.DrawLine(eye, eye + right);
        const int segments = 16;
        Vector3 prev = eye + left;
        for (int i = 1; i <= segments; i++)
        {
            float a = Mathf.Lerp(-viewAngle * 0.5f, viewAngle * 0.5f, i / (float)segments);
            Vector3 p = eye + Quaternion.Euler(0f, a, 0f) * transform.forward * viewDistance;
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
