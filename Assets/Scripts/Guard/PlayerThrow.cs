using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Lets the player carry one piece of rubbish (picked up from the trash can) and throw it with the
/// left mouse button. Where it first lands it makes a noise, and guards that react to noise walk
/// over to look. Lives on the Player object.
///
/// This is a simple stand-in so the guard distraction can be played; the player system can replace it.
/// </summary>
public class PlayerThrow : MonoBehaviour
{
    [Tooltip("Speed the rubbish leaves the hand at, in m/s.")]
    [SerializeField] private float throwSpeed = 9f;
    [Tooltip("Guards within this many metres of where it lands hear it.")]
    [SerializeField] private float noiseRadius = 12f;

    [Header("Events (for sound)")]
    [Tooltip("Raised when the player takes a piece of rubbish from the trash can.")]
    public UnityEvent onPickedUp;
    [Tooltip("Raised when the player throws it. The landing is ThrownNoise.AnyLanded.")]
    public UnityEvent onThrown;

    /// <summary>True while the player has something to throw.</summary>
    public bool IsHolding { get; private set; }

    private Camera _view;
    private GUIStyle _hintStyle;

    private void Awake() => _view = GetComponentInChildren<Camera>();

    /// <summary>Puts a piece of rubbish in the player's hand (called by the trash can). He carries one at a time.</summary>
    public void PickUp()
    {
        if (IsHolding) return;
        IsHolding = true;
        onPickedUp?.Invoke();
    }

    private void Update()
    {
        if (!IsHolding || AlarmPuzzleLauncher.IsOpen) return;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) Throw();
    }

    /// <summary>Throws what the player is holding the way the camera is looking.</summary>
    public void Throw()
    {
        if (!IsHolding || _view == null) return;
        IsHolding = false;

        Transform eye = _view.transform;
        GameObject rubbish = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rubbish.name = "ThrownRubbish";
        rubbish.transform.position = eye.position + eye.forward * 0.6f;
        rubbish.transform.localScale = Vector3.one * 0.14f;
        rubbish.GetComponent<Renderer>().material.color = new Color(0.78f, 0.76f, 0.7f); // crumpled paper

        var body = rubbish.AddComponent<Rigidbody>();
        body.mass = 0.2f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity = eye.forward * throwSpeed + Vector3.up * 1.5f;

        // It must not bounce off the player who threw it.
        Collider thrown = rubbish.GetComponent<Collider>();
        foreach (Collider own in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(thrown, own);

        rubbish.AddComponent<ThrownNoise>().radius = noiseRadius;
        onThrown?.Invoke();
    }

    private void OnGUI()
    {
        if (!IsHolding || AlarmPuzzleLauncher.IsOpen) return;

        if (_hintStyle == null)
        {
            _hintStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
            _hintStyle.normal.textColor = Color.white;
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 170f, Screen.height - 60f, 340f, 30f),
            "Holding rubbish.  Left click to throw it.", _hintStyle);
    }
}
