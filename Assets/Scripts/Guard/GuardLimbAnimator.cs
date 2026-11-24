using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Idle / walk / run for the blocky guard model (Assets/ThirdParty/05_Guard). That model has no
/// animation clips, only rigid parts hanging off named joints, so this swings the joints from code:
/// arms and legs swing in opposite pairs, the knee bends while a leg comes forward, the feet stay
/// level, and the hips drop a little so the planted foot stays on the floor. The size and pace of the motion follow
/// the guard's speed, so idle, walk and run blend into each other without an Animator Controller.
/// While the guard is puzzled by a noise (GuardController.IsScratchingHead) it also lifts his right
/// hand to his head and scratches.
///
/// Lives on the Guard root next to GuardController (the setup tool adds it). Without a
/// GuardController it measures its own movement instead, so it also works on any other character
/// that uses the same joint names, such as the prisoner from the same kit.
/// </summary>
[DisallowMultipleComponent]
public class GuardLimbAnimator : MonoBehaviour
{
    [Serializable]
    public struct Gait
    {
        [Tooltip("How far each leg swings forward and back, in degrees.")]
        public float legSwing;
        [Tooltip("Knee bend while the leg comes forward, in degrees.")]
        public float kneeBend;
        [Tooltip("How far each arm swings, in degrees.")]
        public float armSwing;
        [Tooltip("How much the elbows stay bent, in degrees.")]
        public float elbowBend;
        [Tooltip("Forward lean of the upper body, in degrees.")]
        public float lean;
        [Tooltip("Metres covered by one full cycle (two steps). Smaller = quicker steps.")]
        public float strideLength;

        public static Gait Lerp(Gait a, Gait b, float t) => new Gait
        {
            legSwing = Mathf.Lerp(a.legSwing, b.legSwing, t),
            kneeBend = Mathf.Lerp(a.kneeBend, b.kneeBend, t),
            armSwing = Mathf.Lerp(a.armSwing, b.armSwing, t),
            elbowBend = Mathf.Lerp(a.elbowBend, b.elbowBend, t),
            lean = Mathf.Lerp(a.lean, b.lean, t),
            strideLength = Mathf.Lerp(a.strideLength, b.strideLength, t),
        };
    }

    [Header("Speeds the two gaits are tuned for (m/s)")]
    [Tooltip("Match GuardController's walk speed.")]
    [SerializeField] private float walkSpeed = 1.6f;
    [Tooltip("Match GuardController's run speed.")]
    [SerializeField] private float runSpeed = 3.2f;

    [Header("Gaits")]
    [SerializeField] private Gait walk = new Gait
        { legSwing = 26f, kneeBend = 30f, armSwing = 22f, elbowBend = 8f, lean = 0f, strideLength = 1.3f };
    [SerializeField] private Gait run = new Gait
        { legSwing = 40f, kneeBend = 65f, armSwing = 45f, elbowBend = 55f, lean = 8f, strideLength = 2.2f };

    [Header("Idle")]
    [Tooltip("Small arm sway while standing still, in degrees.")]
    [SerializeField] private float idleArmSway = 2f;
    [Tooltip("Sways per second while standing still.")]
    [SerializeField] private float idleSwayRate = 0.25f;

    [Header("Head scratch (puzzled by a noise)")]
    [Tooltip("How far the right upper arm lifts forward and up, in degrees from hanging.")]
    [SerializeField] private float scratchArmLift = 140f;
    [Tooltip("How far the raised arm leans in toward the head, in degrees.")]
    [SerializeField] private float scratchArmInward = 40f;
    [Tooltip("Elbow bend that brings the hand to the head, in degrees.")]
    [SerializeField] private float scratchElbowBend = 100f;
    [Tooltip("How far the forearm rubs back and forth, in degrees.")]
    [SerializeField] private float scratchRub = 12f;
    [Tooltip("Rubs per second.")]
    [SerializeField] private float scratchRate = 2.5f;
    [Tooltip("How far the head tips down toward what he is looking at, in degrees.")]
    [SerializeField] private float scratchHeadTilt = 14f;
    [Tooltip("Seconds to raise the hand, and to lower it again.")]
    [SerializeField] private float scratchRaiseSeconds = 0.5f;

    [Header("Blending")]
    [Tooltip("How quickly the pose follows a change of speed. Higher = snappier.")]
    [SerializeField] private float blendSharpness = 8f;

    [Header("Events (for sound)")]
    [Tooltip("Raised each time a foot comes down while he walks or runs.")]
    public UnityEvent onFootstep;

    /// <summary>The same moment for every character at once; the animator is passed along, e.g. for its position.</summary>
    public static event Action<GuardLimbAnimator> AnyFootstep;

    private struct Joint
    {
        public Transform t;
        public Quaternion restLocal;
    }

    private const float TwoPi = Mathf.PI * 2f;

    private Joint _hips, _chest, _head;
    private Joint _leftArm, _leftForearm, _rightArm, _rightForearm;
    private Joint _leftThigh, _leftShin, _leftFoot, _rightThigh, _rightShin, _rightFoot;
    private Vector3 _hipsRestLocalPosition;
    private float _thighLength, _shinLength; // hip to knee, knee to ankle, in metres
    private bool _ready;

    private GuardController _guard;
    private Vector3 _lastPosition;
    private float _speed;
    private float _phase;
    private float _idlePhase;
    private float _scratch;       // 0 = arm down .. 1 = hand at the head
    private float _scratchPhase;
    private int _lastHalfCycle = -1;

    // If "Enter Play Mode without domain reload" is on, statics survive between play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => AnyFootstep = null;

    private void Awake() => Bind();

    private void OnEnable() => _lastPosition = transform.position;

    private void OnDisable() => RestorePose();

    /// <summary>Finds the joints and remembers their rest pose. Does nothing if there is no model yet.</summary>
    private void Bind()
    {
        _guard = GetComponentInParent<GuardController>();

        _hips = Find("LowerTorso");
        _chest = Find("UpperTorso");
        _head = Find("Head");
        _leftArm = Find("LeftUpperArm");
        _leftForearm = Find("LeftLowerArm");
        _rightArm = Find("RightUpperArm");
        _rightForearm = Find("RightLowerArm");
        _leftThigh = Find("LeftUpperLeg");
        _leftShin = Find("LeftLowerLeg");
        _leftFoot = Find("LeftFoot");
        _rightThigh = Find("RightUpperLeg");
        _rightShin = Find("RightLowerLeg");
        _rightFoot = Find("RightFoot");

        _ready = _hips.t != null && _leftThigh.t != null && _rightThigh.t != null;
        if (!_ready) return; // placeholder capsule, nothing to animate

        _hipsRestLocalPosition = _hips.t.localPosition;
        // A leg pivots at the hip, bends at the knee and stands on its ankle (the foot below stays level).
        Vector3 hip = _leftThigh.t.position;
        if (_leftShin.t != null && _leftFoot.t != null)
        {
            _thighLength = Vector3.Distance(hip, _leftShin.t.position);
            _shinLength = Vector3.Distance(_leftShin.t.position, _leftFoot.t.position);
        }
        else
        {
            // No knee or ankle joints: treat the leg as one straight piece down to the floor (the root's height).
            _thighLength = Mathf.Max(0.01f, hip.y - transform.position.y);
            _shinLength = 0f;
        }
    }

    private void LateUpdate()
    {
        if (!_ready) return;
        float dt = Time.deltaTime;

        _speed = Mathf.Lerp(_speed, MeasureSpeed(dt), 1f - Mathf.Exp(-blendSharpness * dt));

        float running = Mathf.InverseLerp(walkSpeed, runSpeed, _speed);
        float stride = Mathf.Max(0.1f, Mathf.Lerp(walk.strideLength, run.strideLength, running));
        _phase = Mathf.Repeat(_phase + _speed / stride * TwoPi * dt, TwoPi);
        _idlePhase = Mathf.Repeat(_idlePhase + idleSwayRate * TwoPi * dt, TwoPi);

        // A foot comes down each time the cycle passes a quarter or three-quarter turn: that is when
        // the leading leg is at full reach and both feet are on the floor.
        int halfCycle = Mathf.FloorToInt(Mathf.Repeat(_phase - Mathf.PI * 0.5f, TwoPi) / Mathf.PI);
        if (halfCycle != _lastHalfCycle)
        {
            if (_lastHalfCycle >= 0 && _speed > 0.2f)
            {
                onFootstep?.Invoke();
                AnyFootstep?.Invoke(this);
            }
            _lastHalfCycle = halfCycle;
        }

        bool scratching = _guard != null && _guard.IsScratchingHead;
        _scratch = Mathf.MoveTowards(_scratch, scratching ? 1f : 0f, dt / Mathf.Max(0.05f, scratchRaiseSeconds));
        _scratchPhase = Mathf.Repeat(_scratchPhase + scratchRate * TwoPi * dt, TwoPi);

        ApplyPose(_speed, _phase, _idlePhase, _scratch, _scratchPhase);
    }

    private float MeasureSpeed(float dt)
    {
        if (_guard != null) return _guard.CurrentSpeed;

        Vector3 moved = transform.position - _lastPosition;
        _lastPosition = transform.position;
        moved.y = 0f;
        // The cap keeps a teleport (level restart) from showing up as a burst of sprinting.
        return dt > 0f ? Mathf.Min(moved.magnitude / dt, runSpeed * 1.5f) : 0f;
    }

    /// <summary>
    /// Poses every joint for one moment of the cycle. Angles are measured around the character's
    /// left-right axis. <paramref name="scratch"/> (0..1) blends the right arm up into the head-scratch.
    /// </summary>
    private void ApplyPose(float speed, float phase, float idlePhase, float scratch, float scratchPhase)
    {
        float moving = Mathf.Clamp01(speed / Mathf.Max(0.01f, walkSpeed)); // 0 standing .. 1 walking or faster
        Gait gait = Gait.Lerp(walk, run, Mathf.InverseLerp(walkSpeed, runSpeed, speed));

        float leg = gait.legSwing * moving;
        float knee = gait.kneeBend * moving;
        float arm = gait.armSwing * moving;
        float elbow = gait.elbowBend * moving;
        float sway = Mathf.Sin(idlePhase) * idleArmSway * (1f - moving);

        float s = Mathf.Sin(phase);
        float c = Mathf.Cos(phase);

        // A positive angle swings a hanging limb backwards. The knee bends while its leg comes forward.
        float leftThigh = -leg * s, leftKnee = knee * Mathf.Max(0f, c);
        float rightThigh = leg * s, rightKnee = knee * Mathf.Max(0f, -c);

        // Lower the hips until the leg that reaches further down has its ankle back at standing
        // height. That foot is then planted on the floor and the other one is in the air.
        float reach = Mathf.Max(LegReach(leftThigh, leftKnee), LegReach(rightThigh, rightKnee));
        Vector3 down = Vector3.down * (_thighLength + _shinLength - reach);
        Transform hipsParent = _hips.t.parent;
        _hips.t.localPosition = _hipsRestLocalPosition + (hipsParent != null ? hipsParent.InverseTransformVector(down) : down);

        // Parents are posed before their children.
        Pose(_chest, gait.lean * moving);

        Pose(_leftThigh, leftThigh);
        Pose(_leftShin, leftKnee);
        Pose(_leftFoot, -(leftThigh + leftKnee)); // undo the leg's tilt so the sole stays flat
        Pose(_rightThigh, rightThigh);
        Pose(_rightShin, rightKnee);
        Pose(_rightFoot, -(rightThigh + rightKnee));

        Pose(_leftArm, arm * s + sway);
        Pose(_leftForearm, -elbow);

        // Right arm: its normal swing, eased up into the head-scratch. Lifting is a negative angle
        // (forward), and for the right arm leaning in toward the head is a negative roll.
        float ease = scratch * scratch * (3f - 2f * scratch);
        float rub = Mathf.Sin(scratchPhase) * scratchRub;
        Pose(_rightArm, Mathf.Lerp(-arm * s - sway, -scratchArmLift, ease), Mathf.Lerp(0f, -scratchArmInward, ease));
        Pose(_rightForearm, Mathf.Lerp(-elbow, -scratchElbowBend + rub, ease));
        Pose(_head, scratchHeadTilt * ease);
    }

    /// <summary>How far below the hip the ankle is for the given thigh and knee angles (degrees).</summary>
    private float LegReach(float thigh, float knee) =>
        _thighLength * Mathf.Cos(thigh * Mathf.Deg2Rad) + _shinLength * Mathf.Cos((thigh + knee) * Mathf.Deg2Rad);

    /// <summary>
    /// Turns a joint by <paramref name="angle"/> degrees from its rest pose around the character's
    /// left-right axis, after first leaning it <paramref name="roll"/> degrees sideways around his
    /// front-back axis (positive leans a hanging limb toward his right).
    /// Done in world space because the model's joints each have their own odd local axes.
    /// </summary>
    private void Pose(Joint joint, float angle, float roll = 0f)
    {
        if (joint.t == null) return;
        Transform parent = joint.t.parent;
        Quaternion rest = parent != null ? parent.rotation * joint.restLocal : joint.restLocal;
        Quaternion turn = Quaternion.AngleAxis(angle, transform.right);
        if (roll != 0f) turn *= Quaternion.AngleAxis(roll, transform.forward);
        joint.t.rotation = turn * rest;
    }

    private void RestorePose()
    {
        if (!_ready) return;
        _hips.t.localPosition = _hipsRestLocalPosition;
        Restore(_hips); Restore(_chest); Restore(_head);
        Restore(_leftArm); Restore(_leftForearm); Restore(_rightArm); Restore(_rightForearm);
        Restore(_leftThigh); Restore(_leftShin); Restore(_leftFoot);
        Restore(_rightThigh); Restore(_rightShin); Restore(_rightFoot);
    }

    private static void Restore(Joint joint)
    {
        if (joint.t != null) joint.t.localRotation = joint.restLocal;
    }

    private Joint Find(string jointName)
    {
        Transform t = FindJoint(transform, jointName);
        return new Joint { t = t, restLocal = t != null ? t.localRotation : Quaternion.identity };
    }

    /// <summary>
    /// Finds a joint by name anywhere under <paramref name="root"/>. In this model a joint and the
    /// mesh hanging off it share a name (LeftFoot > LeftFoot); searching parents first returns the joint.
    /// </summary>
    public static Transform FindJoint(Transform root, string jointName)
    {
        foreach (Transform child in root)
        {
            if (child.name == jointName) return child;
            Transform found = FindJoint(child, jointName);
            if (found != null) return found;
        }
        return null;
    }
}
