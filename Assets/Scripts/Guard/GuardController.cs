using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

public enum GuardState
{
    Patrolling,     // walking between patrol waypoints, or back to his post
    Idle,           // pausing at a waypoint, looking around
    CheckingNoise,  // walking over to a noise he heard, then looking around there
    Distracted,     // running along the distraction route (alarm went off)
    Investigating,  // standing in the distraction room for a while
    Returning,      // walking back along the distraction route to the patrol
    Alert           // saw something suspicious and stopped
}

/// <summary>
/// A prison guard's preset behaviour. There can be any number of guards in a scene; each one
/// patrols his own waypoints (a single waypoint makes him stand at a post), pauses at each one,
/// leaves for the distraction room when <see cref="Distract()"/> is called, then comes back.
/// A guard with "Reacts To Noise" on also walks over to check a thrown object.
///
/// Integration points for the rest of the team:
///   - Alarm / puzzles:   call Distract() on each guard. GuardAlarmLink already does that for every guard whenever
///                        AlarmComputerInteractable.onSolved is raised, without touching the alarm computer object.
///                        Calling it again while he waits in the distraction room tops the wait back up.
///   - Thrown objects:    call GuardController.MakeNoise(position, radius); guards in range walk over to look
///   - Escape window:     check IsAwayFromPost, or listen to onLeftPost / onReturnedToPost
///   - Fail condition:    listen to onPrisonerSpotted, or to the static AnyGuardSpotted for every guard at once
///                        (raised by GuardVision when it sees a suspicious GuardDetectable)
///   - Restart:           call ResetGuard()
///   - Doors:             he opens the doors on his route (GuardDoor) as he reaches them and shuts them behind him
///   - Collision:         he has a kinematic Rigidbody + CapsuleCollider, so the player can't walk through him,
///                        and he stops and waits (IsBlocked) while the player or prisoner stands in his way
///   - Sound / UI:        every moment worth a sound has an event to hang it on; Assets/Scripts/Guard/README.md lists them
///   - Animation:         GuardLimbAnimator reads CurrentSpeed and moves the model's joints;
///                        an Animator anywhere under the guard also gets a float "Speed" parameter (m/s)
///
/// Movement follows the waypoints in straight lines (no NavMesh needed), so waypoints
/// must have a clear line between consecutive points.
/// </summary>
[DisallowMultipleComponent]
public class GuardController : MonoBehaviour
{
    [Header("Patrol")]
    [Tooltip("Visited in order, then loops back to the first. The same waypoint can appear more than once. A single waypoint is a post he stands at.")]
    [SerializeField] private List<Transform> patrolWaypoints = new List<Transform>();
    [SerializeField] private float walkSpeed = 1.6f;
    [Tooltip("Degrees per second.")]
    [SerializeField] private float turnSpeed = 360f;
    [Tooltip("Random pause (seconds) at each waypoint.")]
    [SerializeField] private Vector2 idleTimeRange = new Vector2(1.5f, 3.5f);
    [Tooltip("How far (degrees) the guard glances left/right while pausing at a waypoint or investigating.")]
    [SerializeField] private float lookAroundAngle = 50f;
    [Tooltip("Left-right glances per second while pausing at a waypoint. Lower is calmer.")]
    [SerializeField] private float lookAroundRate = 0.6f;
    [Tooltip("Degrees per second he turns while standing: glancing around, or turning to face his post. Lower is calmer.")]
    [SerializeField] private float standingTurnSpeed = 180f;
    [Tooltip("While pausing at a waypoint, face the way the waypoint points (its blue arrow) instead of the way he walked in. Turn on for a guard who watches from a fixed post.")]
    [SerializeField] private bool faceWaypointDirection;

    [Header("Alarm distraction")]
    [Tooltip("Path from the patrol to the distraction spot. The LAST point is where the guard waits.")]
    [SerializeField] private List<Transform> distractionRoute = new List<Transform>();
    [SerializeField] private float runSpeed = 3.2f;
    [Tooltip("Seconds the guard stays at the distraction spot before returning.")]
    [SerializeField] private float distractionDuration = 15f;
    [Tooltip("Doors on the distraction route. Each opens as the guard reaches it while he answers the alarm and closes behind him.")]
    [SerializeField] private List<GuardDoor> routeDoors = new List<GuardDoor>();
    [Tooltip("How close (metres) the guard gets to a door before it opens.")]
    [SerializeField] private float doorOpenDistance = 2.5f;

    [Header("Noise distraction")]
    [Tooltip("Walk over and look when a noise is made nearby (GuardController.MakeNoise), such as a thrown object landing.")]
    [SerializeField] private bool reactsToNoise;
    [Tooltip("Seconds he stands where the noise came from, puzzled, before going back.")]
    [SerializeField] private float noiseCheckSeconds = 8f;
    [Tooltip("He stops this far (metres) short of the noise, or earlier if a wall or furniture is in the way.")]
    [SerializeField] private float noiseStopDistance = 1f;

    [Header("Collision")]
    [Tooltip("The guard stops and waits when the player or prisoner is this close in front of him. Needs a CapsuleCollider on the guard.")]
    [SerializeField] private float personalSpace = 0.3f;
    [Tooltip("Layers checked for things in his way.")]
    [FormerlySerializedAs("characterMask")]
    [SerializeField] private LayerMask sweepMask = ~0;

    [Header("Events")]
    public UnityEvent onLeftPost;          // alarm reaction started
    public UnityEvent onReachedDistraction;
    public UnityEvent onReturnedToPost;    // back on patrol after the alarm
    public UnityEvent onHeardNoise;        // started walking over to a noise
    public UnityEvent onPuzzled;           // reached the noise, found nothing, starts scratching his head
    public UnityEvent onPrisonerSpotted;   // saw something suspicious (raised via GuardVision)

    /// <summary>Fires whenever the state changes (old, new).</summary>
    public event Action<GuardState, GuardState> StateChanged;

    // The same moments for every guard at once, for code that wants one listener (sound, UI) instead of
    // hooking each guard in the Inspector. The guard that raised it is passed along, e.g. for its position.

    /// <summary>Raised when any guard sees something suspicious: the guard, and what he saw.</summary>
    public static event Action<GuardController, Transform> AnyGuardSpotted;

    /// <summary>Raised when any guard hears a noise and starts walking over to it.</summary>
    public static event Action<GuardController> AnyGuardHeardNoise;

    /// <summary>Raised when any guard, standing at a noise, starts scratching his head.</summary>
    public static event Action<GuardController> AnyGuardPuzzled;

    /// <summary>Every enabled guard in the loaded scenes.</summary>
    public static IReadOnlyList<GuardController> All => _all;

    public GuardState State { get; private set; } = GuardState.Patrolling;

    /// <summary>True from the moment the alarm fires until the guard is back on patrol.</summary>
    public bool IsAwayFromPost =>
        State == GuardState.Distracted || State == GuardState.Investigating || State == GuardState.Returning;

    /// <summary>Seconds left before the guard starts heading back (0 when not investigating).</summary>
    public float DistractionTimeRemaining => State == GuardState.Investigating ? Mathf.Max(0f, _timer) : 0f;

    /// <summary>How fast the guard is moving this frame, in m/s (0 while standing or turning on the spot).</summary>
    public float CurrentSpeed => _currentSpeed;

    /// <summary>True while the player or prisoner is standing in the guard's way and he is waiting for them to move.</summary>
    public bool IsBlocked { get; private set; }

    /// <summary>
    /// True while he stands at a noise he went to check, scratching his head (GuardLimbAnimator plays it).
    /// It starts a beat after he arrives and ends a moment before he walks off, so the arm is down in time.
    /// </summary>
    public bool IsScratchingHead =>
        State == GuardState.CheckingNoise && _noiseReached && _timer > 1f && _timer < noiseCheckSeconds - 0.4f;

    private const float ArriveDistance = 0.1f;
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly List<GuardController> _all = new List<GuardController>();

    private int _patrolIndex;
    private int _routeIndex;
    private float _timer;
    private float _pendingDuration;
    private float _currentSpeed;
    private float _lookBaseYaw;
    private float _lookPhase;
    private Transform _alertTarget;
    private Vector3 _noisePoint;
    private bool _noiseReached;
    private bool _wasScratching;

    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Animator _animator;
    private bool _animatorHasSpeed;
    private CapsuleCollider _capsule;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    // If "Enter Play Mode without domain reload" is on, statics survive between play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _all.Clear();
        AnyGuardSpotted = null;
        AnyGuardHeardNoise = null;
        AnyGuardPuzzled = null;
    }

    private void Awake()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;
        _capsule = GetComponent<CapsuleCollider>();

        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            foreach (var p in _animator.parameters)
                if (p.nameHash == SpeedParam && p.type == AnimatorControllerParameterType.Float) _animatorHasSpeed = true;
        }
    }

    private void OnEnable()
    {
        if (!_all.Contains(this)) _all.Add(this);
    }

    private void OnDisable()
    {
        _all.Remove(this);
        ReleaseDoors();
    }

    private void Start() => ResetGuard();

    // ------------------------------------------------------------------ public API

    /// <summary>Send the guard to the distraction spot (hook this to the alarm). Uses the Inspector duration.</summary>
    public void Distract() => Distract(distractionDuration);

    /// <summary>Send the guard to the distraction spot and keep him there for <paramref name="duration"/> seconds.</summary>
    public void Distract(float duration)
    {
        if (State == GuardState.Alert) return; // too late, he already saw something
        if (distractionRoute.Count == 0 || distractionRoute[distractionRoute.Count - 1] == null)
        {
            Debug.LogWarning("[Guard] Distract() called but the distraction route is empty.", this);
            return;
        }

        _pendingDuration = duration;

        switch (State)
        {
            case GuardState.Investigating:
                _timer = Mathf.Max(_timer, duration); // already there: top the wait back up
                return;
            case GuardState.Distracted:
                return; // already on the way
            case GuardState.Returning:
                // He's somewhere on the route heading back to point _routeIndex; turn around.
                _routeIndex = Mathf.Min(_routeIndex + 1, distractionRoute.Count - 1);
                break;
            default:
                _routeIndex = 0;
                onLeftPost?.Invoke();
                break;
        }

        SetState(GuardState.Distracted);
    }

    /// <summary>
    /// Makes a noise at <paramref name="position"/>. Every guard with "Reacts To Noise" on and within
    /// <paramref name="radius"/> metres walks over to look. Call it when a thrown object lands.
    /// </summary>
    public static void MakeNoise(Vector3 position, float radius)
    {
        for (int i = 0; i < _all.Count; i++)
        {
            GuardController guard = _all[i];
            if (!guard.reactsToNoise) continue;
            if (Flatten(position - guard.transform.position, 0f).magnitude <= radius) guard.HearNoise(position);
        }
    }

    /// <summary>Sends this guard over to look at a noise, unless he is busy with the alarm or has already caught someone.</summary>
    public void HearNoise(Vector3 position)
    {
        if (State == GuardState.Alert || IsAwayFromPost) return;

        _noisePoint = Flatten(position, transform.position.y);
        _noiseReached = false;
        SetState(GuardState.CheckingNoise);
        onHeardNoise?.Invoke();
        AnyGuardHeardNoise?.Invoke(this);
    }

    /// <summary>Put the guard back at the start of his patrol (call on level restart).</summary>
    public void ResetGuard()
    {
        _alertTarget = null;
        IsBlocked = false;
        ReleaseDoors();
        foreach (GuardDoor door in routeDoors)
            if (door != null && !door.IsOpen) door.Snap(false); // shut at once, unless another guard is using it

        Transform first = FirstValid(patrolWaypoints);
        if (first != null)
        {
            transform.position = Flatten(first.position, transform.position.y);
            Transform next = patrolWaypoints.Count > 1 ? patrolWaypoints[1] : null;
            if (next != null && (next.position - first.position).sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(Flatten(next.position - first.position, 0f));
            else if (faceWaypointDirection)
                transform.rotation = Quaternion.Euler(0f, first.eulerAngles.y, 0f);
            _patrolIndex = patrolWaypoints.Count > 1 ? 1 : 0;
        }
        else
        {
            transform.SetPositionAndRotation(_startPosition, _startRotation);
            _patrolIndex = 0;
        }

        _currentSpeed = 0f;
        SetState(GuardState.Patrolling);
    }

    /// <summary>Called by GuardVision when something suspicious is seen. Stops the guard and raises the spotted events.</summary>
    public void ReportSpotted(Transform target)
    {
        if (State == GuardState.Alert) return;
        _alertTarget = target;
        SetState(GuardState.Alert);
        onPrisonerSpotted?.Invoke();
        AnyGuardSpotted?.Invoke(this, target);
    }

    // ------------------------------------------------------------------ state machine

    private void Update()
    {
        _currentSpeed = 0f;
        IsBlocked = false;

        switch (State)
        {
            case GuardState.Patrolling: UpdatePatrolling(); break;
            case GuardState.Idle: UpdateIdle(); break;
            case GuardState.CheckingNoise: UpdateCheckingNoise(); break;
            case GuardState.Distracted: UpdateDistracted(); break;
            case GuardState.Investigating: UpdateInvestigating(); break;
            case GuardState.Returning: UpdateReturning(); break;
            case GuardState.Alert: UpdateAlert(); break;
        }

        UpdateDoors();

        bool scratching = IsScratchingHead;
        if (scratching && !_wasScratching)
        {
            onPuzzled?.Invoke();
            AnyGuardPuzzled?.Invoke(this);
        }
        _wasScratching = scratching;

        if (_animatorHasSpeed) _animator.SetFloat(SpeedParam, _currentSpeed);
    }

    private void UpdatePatrolling()
    {
        Transform target = patrolWaypoints.Count > 0 ? patrolWaypoints[_patrolIndex] : null;
        if (target == null) { SkipToNextWaypoint(); return; }

        if (MoveTo(target.position, walkSpeed))
        {
            _timer = UnityEngine.Random.Range(idleTimeRange.x, idleTimeRange.y);
            BeginLookAround(faceWaypointDirection ? target.eulerAngles.y : transform.eulerAngles.y);
            SetState(GuardState.Idle);
        }
    }

    private void UpdateIdle()
    {
        LookAround(lookAroundRate);
        if (patrolWaypoints.Count <= 1) return; // a post, not a patrol: he just keeps watching from here

        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            SkipToNextWaypoint();
            SetState(GuardState.Patrolling);
        }
    }

    private void UpdateCheckingNoise()
    {
        if (_noiseReached)
        {
            // Stand still facing it (GuardLimbAnimator adds the head-scratch), then give up and go back.
            TurnTowardsYaw(_lookBaseYaw);
            _timer -= Time.deltaTime;
            if (_timer <= 0f) SetState(GuardState.Patrolling); // carries on to the waypoint he was heading for
            return;
        }

        Vector3 to = _noisePoint - transform.position;
        float distance = to.magnitude;
        // The small margin matters: without it he can end up a hair outside the stopping distance,
        // taking steps too small to move him, and walk on the spot for ever.
        float remaining = distance - noiseStopDistance;
        if (remaining > ArriveDistance * 0.5f)
        {
            Vector3 direction = to / distance;
            RotateTowards(direction);
            float step = Mathf.Min(walkSpeed * Time.deltaTime, remaining);
            Sweep(direction, step, out bool character, out bool obstacle);
            if (character)
            {
                IsBlocked = true;
                return;
            }
            if (!obstacle)
            {
                transform.position += direction * step;
                _currentSpeed = walkSpeed;
                return;
            }
            // A wall or a piece of furniture is in the way: this is as close as he gets.
        }

        _noiseReached = true;
        _timer = noiseCheckSeconds;
        BeginLookAround(distance > 0.05f ? Quaternion.LookRotation(to).eulerAngles.y : transform.eulerAngles.y);
    }

    private void UpdateDistracted()
    {
        Transform target = distractionRoute[_routeIndex];
        if (target == null || MoveTo(target.position, runSpeed))
        {
            if (_routeIndex < distractionRoute.Count - 1) { _routeIndex++; return; }

            _timer = _pendingDuration;
            BeginLookAround(transform.eulerAngles.y);
            SetState(GuardState.Investigating);
            onReachedDistraction?.Invoke();
        }
    }

    private void UpdateInvestigating()
    {
        LookAround(0.4f);
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            _routeIndex = distractionRoute.Count - 2; // head back toward the previous route point
            SetState(GuardState.Returning);
            if (_routeIndex < 0) FinishReturning();
        }
    }

    private void UpdateReturning()
    {
        Transform target = distractionRoute[_routeIndex];
        if (target == null || MoveTo(target.position, walkSpeed))
        {
            _routeIndex--;
            if (_routeIndex < 0) FinishReturning();
        }
    }

    private void FinishReturning()
    {
        _patrolIndex = NearestPatrolIndex();
        SetState(GuardState.Patrolling);
        onReturnedToPost?.Invoke();
    }

    private void UpdateAlert()
    {
        if (_alertTarget == null) return;
        Vector3 to = Flatten(_alertTarget.position - transform.position, 0f);
        if (to.sqrMagnitude > 0.0001f) RotateTowards(to);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Moves toward a point on the guard's own height. Returns true on arrival.</summary>
    private bool MoveTo(Vector3 worldTarget, float speed)
    {
        Vector3 target = Flatten(worldTarget, transform.position.y);
        Vector3 to = target - transform.position;
        float distance = to.magnitude;
        if (distance <= ArriveDistance) return true;

        RotateTowards(to);
        float step = Mathf.Min(speed * Time.deltaTime, distance);
        // Walls and props are not checked here on purpose: the waypoints are laid out with clear
        // lines between them, so the only thing that can turn up in the way is a character.
        Sweep(to / distance, step, out bool character, out _);
        if (character)
        {
            IsBlocked = true; // wait facing the target; the speed stays 0, so he stands idle
            return false;
        }

        transform.position = Vector3.MoveTowards(transform.position, target, step);
        _currentSpeed = speed;
        return (target - transform.position).magnitude <= ArriveDistance;
    }

    /// <summary>
    /// Sweeps the guard's own capsule along the next step and reports what it would run into:
    /// a character (the player, or anything with a GuardDetectable such as the prisoner) or a solid
    /// obstacle (a wall, furniture, a shut door). Other guards and loose objects such as a thrown
    /// can are ignored, so guards walk past each other instead of blocking one another.
    /// </summary>
    private void Sweep(Vector3 direction, float step, out bool character, out bool obstacle)
    {
        character = false;
        obstacle = false;
        if (_capsule == null) return;

        float radius = _capsule.radius * 0.9f;
        float halfSpan = Mathf.Max(0f, _capsule.height * 0.5f - _capsule.radius);
        Vector3 centre = transform.TransformPoint(_capsule.center);
        Vector3 top = centre + Vector3.up * halfSpan;
        Vector3 bottom = centre - Vector3.up * halfSpan;

        int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, _hits,
            step + personalSpace, sweepMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = _hits[i].collider;
            if (hit.transform.IsChildOf(transform)) continue;                    // the guard himself
            if (hit.GetComponentInParent<GuardController>() != null) continue;   // another guard

            if (hit is CharacterController || hit.GetComponentInParent<GuardDetectable>() != null) character = true;
            else if (hit.attachedRigidbody == null) obstacle = true;
        }
    }

    /// <summary>
    /// Holds each door on the route open while the guard is passing through it on the way to or from
    /// the distraction spot, and lets go once he is clear. Doors stay shut on patrol and while he
    /// waits inside. A door two guards use stays open until both have let go.
    /// </summary>
    private void UpdateDoors()
    {
        bool onRoute = State == GuardState.Distracted || State == GuardState.Returning;
        for (int i = 0; i < routeDoors.Count; i++)
        {
            GuardDoor door = routeDoors[i];
            if (door == null) continue;
            bool passing = onRoute
                && Flatten(door.transform.position - transform.position, 0f).magnitude <= doorOpenDistance;
            door.SetHeldOpen(this, passing);
        }
    }

    private void ReleaseDoors()
    {
        for (int i = 0; i < routeDoors.Count; i++)
            if (routeDoors[i] != null) routeDoors[i].SetHeldOpen(this, false);
    }

    private void RotateTowards(Vector3 direction)
    {
        Quaternion look = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void BeginLookAround(float baseYaw)
    {
        _lookBaseYaw = baseYaw;
        _lookPhase = 0f;
    }

    private void LookAround(float cyclesPerSecond)
    {
        _lookPhase += Time.deltaTime * cyclesPerSecond * Mathf.PI * 2f;
        // Two overlapping swings, so the glances don't look like a metronome.
        float swing = Mathf.Sin(_lookPhase) * 0.75f + Mathf.Sin(_lookPhase * 2.3f) * 0.25f;
        TurnTowardsYaw(_lookBaseYaw + swing * lookAroundAngle);
    }

    /// <summary>Turns on the spot toward a heading, no faster than the standing turn speed.</summary>
    private void TurnTowardsYaw(float yaw)
    {
        Quaternion look = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, standingTurnSpeed * Time.deltaTime);
    }

    private void SkipToNextWaypoint()
    {
        if (patrolWaypoints.Count == 0) return;
        _patrolIndex = (_patrolIndex + 1) % patrolWaypoints.Count;
    }

    private int NearestPatrolIndex()
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < patrolWaypoints.Count; i++)
        {
            if (patrolWaypoints[i] == null) continue;
            float d = (Flatten(patrolWaypoints[i].position, 0f) - Flatten(transform.position, 0f)).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private void SetState(GuardState next)
    {
        if (next == State) return;
        GuardState previous = State;
        State = next;
        StateChanged?.Invoke(previous, next);
    }

    private static Transform FirstValid(List<Transform> list)
    {
        foreach (var t in list) if (t != null) return t;
        return null;
    }

    private static Vector3 Flatten(Vector3 v, float y) => new Vector3(v.x, y, v.z);

    // Right-click the component header in Play mode to test without solving the puzzle.
    [ContextMenu("Test: Distract Now")]
    private void ContextDistract() => Distract();

    [ContextMenu("Test: Make Noise 3 m Behind Him")]
    private void ContextNoise() => HearNoise(transform.position - transform.forward * 3f);

    [ContextMenu("Test: Reset Guard")]
    private void ContextReset() => ResetGuard();

    private void OnDrawGizmos()
    {
        DrawPath(patrolWaypoints, new Color(0.2f, 0.9f, 1f), loop: true);
        DrawPath(distractionRoute, new Color(1f, 0.55f, 0.1f), loop: false);
    }

    private static void DrawPath(List<Transform> points, Color color, bool loop)
    {
        Gizmos.color = color;
        Transform prev = null, first = null;
        foreach (var p in points)
        {
            if (p == null) continue;
            Gizmos.DrawWireSphere(p.position + Vector3.up * 0.1f, 0.25f);
            if (prev != null) Gizmos.DrawLine(prev.position + Vector3.up * 0.1f, p.position + Vector3.up * 0.1f);
            if (first == null) first = p;
            prev = p;
        }
        if (loop && prev != null && first != null && prev != first)
            Gizmos.DrawLine(prev.position + Vector3.up * 0.1f, first.position + Vector3.up * 0.1f);
    }
}
