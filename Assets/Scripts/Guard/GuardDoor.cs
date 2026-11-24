using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A door that guards open for themselves, such as the one to the distraction room or the
/// guard-room door.
///
/// It sits on the invisible blocker in the doorway and drives the visible door model beside it:
/// the model's built-in "open" clip is run forwards to open and backwards to close, and the
/// blocker's collider is switched off while the door is open.
///
/// Each guard holds the door open while he passes (SetHeldOpen), so a door two guards share stays
/// open until both are through. Anything else can use it too: SetOpen(true/false) to swing it, or
/// Snap(...) to change it at once.
/// </summary>
[DisallowMultipleComponent]
public class GuardDoor : MonoBehaviour
{
    [Tooltip("The Animation component on the visible door model (the Door_Model under the Real_..._Door object).")]
    [SerializeField] private Animation doorAnimation;
    [Tooltip("Name of the clip that opens the door.")]
    [SerializeField] private string clipName = "open";
    [Tooltip("Seconds the door takes to open or to close.")]
    [SerializeField] private float openSeconds = 0.6f;
    [Tooltip("Collider that blocks the doorway while the door is shut. Left empty, this object's own collider is used.")]
    [SerializeField] private Collider blocker;

    [Header("Events (for sound)")]
    [Tooltip("Raised when the door starts to swing open.")]
    public UnityEvent onOpened;
    [Tooltip("Raised when the door has swung fully shut.")]
    public UnityEvent onShut;

    /// <summary>The same two moments for every door at once; the door is passed along, e.g. for its position.</summary>
    public static event Action<GuardDoor> AnyDoorOpened;
    public static event Action<GuardDoor> AnyDoorShut;

    /// <summary>True once the door has been told to open, including while it is still swinging.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>0 when fully shut, 1 when fully open.</summary>
    public float OpenAmount { get; private set; }

    private readonly HashSet<object> _holders = new HashSet<object>();
    private AnimationState _clip;

    // If "Enter Play Mode without domain reload" is on, statics survive between play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        AnyDoorOpened = null;
        AnyDoorShut = null;
    }

    private void Awake()
    {
        if (blocker == null) blocker = GetComponent<Collider>();
        if (doorAnimation != null && !string.IsNullOrEmpty(clipName)) _clip = doorAnimation[clipName];
        if (doorAnimation != null && _clip == null)
            Debug.LogWarning($"[GuardDoor] '{doorAnimation.name}' has no clip named '{clipName}', so the door won't visibly move.", this);
        Snap(false);
    }

    /// <summary>
    /// <paramref name="holder"/> starts or stops holding the door open. The door swings open while
    /// at least one holder has it, and shuts when the last one lets go. Safe to call every frame.
    /// </summary>
    public void SetHeldOpen(object holder, bool open)
    {
        bool changed = open ? _holders.Add(holder) : _holders.Remove(holder);
        if (!changed) return;

        bool wasOpen = IsOpen;
        ApplyHolders();
        if (IsOpen && !wasOpen)
        {
            onOpened?.Invoke();
            AnyDoorOpened?.Invoke(this);
        }
    }

    /// <summary>Swings the door open or shut on behalf of the door itself (for buttons, events and tests).</summary>
    public void SetOpen(bool open) => SetHeldOpen(this, open);

    /// <summary>
    /// Puts the door fully open or fully shut at once and forgets who was holding it (used when the
    /// level starts or restarts). This is silent: it raises neither event.
    /// </summary>
    public void Snap(bool open)
    {
        _holders.Clear();
        if (open) _holders.Add(this);
        ApplyHolders();
        OpenAmount = open ? 1f : 0f;
        ShowAmount();
    }

    private void ApplyHolders()
    {
        IsOpen = _holders.Count > 0;
        // The doorway is passable as soon as the door starts to open and blocked as soon as it starts to shut.
        if (blocker != null) blocker.enabled = !IsOpen;
    }

    private void Update()
    {
        float target = IsOpen ? 1f : 0f;
        if (Mathf.Approximately(OpenAmount, target)) return;
        OpenAmount = Mathf.MoveTowards(OpenAmount, target, Time.deltaTime / Mathf.Max(0.01f, openSeconds));
        ShowAmount();

        if (!IsOpen && OpenAmount <= 0f) // this is the frame it finishes closing
        {
            onShut?.Invoke();
            AnyDoorShut?.Invoke(this);
        }
    }

    /// <summary>
    /// Poses the door model part-way through its clip. The clip is sampled by hand instead of played,
    /// because a played clip snaps back to its first frame when it ends.
    /// </summary>
    private void ShowAmount()
    {
        if (_clip == null || !doorAnimation.isActiveAndEnabled) return;
        _clip.enabled = true;
        _clip.weight = 1f;
        _clip.time = OpenAmount * _clip.length;
        doorAnimation.Sample();
        _clip.enabled = false;
    }
}
