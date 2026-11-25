using System;
using UnityEngine;

/// <summary>
/// On a thrown object: the first thing it hits makes a noise there, which sends nearby guards over
/// to look (GuardController.MakeNoise). After that it is just litter, and tidies itself away later.
/// </summary>
public class ThrownNoise : MonoBehaviour
{
    [Tooltip("Guards within this many metres of the impact hear it.")]
    public float radius = 12f;
    [Tooltip("Seconds after landing before the object is removed.")]
    public float lifetime = 30f;

    /// <summary>Raised when any thrown object first lands, with the point it hit (for the landing sound).</summary>
    public static event Action<Vector3> AnyLanded;

    private bool _landed;

    // If "Enter Play Mode without domain reload" is on, statics survive between play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyLanded = null;

    private void OnCollisionEnter(Collision collision)
    {
        if (_landed) return;
        _landed = true;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        AnyLanded?.Invoke(point);
        GuardController.MakeNoise(point, radius);
        Destroy(gameObject, lifetime);
    }
}
