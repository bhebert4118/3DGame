#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Development-only spectator view of the guards, for watching them work while the in-game CCTV
/// feed doesn't exist yet. It needs no setup: it creates itself when Play mode starts and shows
/// nothing until you press Tab. It is left out of release builds entirely.
///
///     Tab:  off  ->  small picture in the top-right corner  ->  full screen  ->  off
///     G:    switch to the next guard
///
/// The view looks straight down on a guard and follows him. The camera's near clip plane is set
/// just above the wall tops, which slices the ceilings away so you can see into the rooms.
/// The player keeps control the whole time, so you can solve the alarm puzzle and watch the result.
/// </summary>
public class GuardSpectatorView : MonoBehaviour
{
    private enum Mode { Off, Inset, Full }

    private const float CameraHeight = 14f;        // metres above the guard's feet
    private const float CeilingCutHeight = 3.05f;  // the wall tops are at 3.0 m and the ceiling tops at 3.1 m
    private const float FieldOfView = 50f;         // shows about 13 m of floor from top to bottom
    private static readonly Rect InsetRect = new Rect(0.66f, 0.56f, 0.33f, 0.42f); // viewport: left, bottom, width, height

    private Mode _mode = Mode.Off;
    private int _guardIndex;
    private Camera _camera;
    private GUIStyle _labelStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("GuardSpectatorView (dev only)");
        DontDestroyOnLoad(go);
        go.AddComponent<GuardSpectatorView>();
    }

    private void Awake()
    {
        var cameraObject = new GameObject("GuardSpectatorCamera");
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down, +Z (the guard room) at the top

        _camera = cameraObject.AddComponent<Camera>();
        _camera.enabled = false;
        _camera.depth = 50f; // drawn after the player's camera, so it sits on top of it
        _camera.fieldOfView = FieldOfView;
        _camera.nearClipPlane = CameraHeight - CeilingCutHeight;
        _camera.farClipPlane = CameraHeight + 5f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
    }

    /// <summary>The guard being watched, or null if the scene has none.</summary>
    private GuardController Watched
    {
        get
        {
            var guards = GuardController.All;
            return guards.Count == 0 ? null : guards[_guardIndex % guards.Count];
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            _mode = (Mode)(((int)_mode + 1) % 3);
            _camera.rect = _mode == Mode.Full ? new Rect(0f, 0f, 1f, 1f) : InsetRect;
        }
        if (_mode != Mode.Off && Keyboard.current.gKey.wasPressedThisFrame) _guardIndex++;
    }

    private void LateUpdate()
    {
        GuardController guard = _mode != Mode.Off ? Watched : null;
        bool show = guard != null;
        if (_camera.enabled != show) _camera.enabled = show;
        if (!show) return;

        Vector3 at = guard.transform.position;
        _camera.transform.position = new Vector3(at.x, at.y + CameraHeight, at.z);
    }

    private void OnGUI()
    {
        if (_mode == Mode.Off) return;

        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.padding = new RectOffset(8, 8, 4, 4);
        }

        // GUI coordinates start at the top-left; the viewport rect starts at the bottom-left.
        Rect view = _camera.rect;
        float left = view.x * Screen.width + 6f;
        float top = (1f - view.yMax) * Screen.height + 6f;

        GUI.color = Color.white;
        GUI.Label(new Rect(left, top, 420f, 26f), Describe(Watched), _labelStyle);
    }

    private static string Describe(GuardController guard)
    {
        if (guard == null) return "GUARD CAM (Tab)   no guard in this scene";

        string doing;
        if (guard.IsBlocked) doing = guard.State + ", waiting for someone to move";
        else if (guard.State == GuardState.Investigating)
            doing = "Investigating, " + Mathf.CeilToInt(guard.DistractionTimeRemaining) + " s left";
        else doing = guard.State.ToString();

        string switchHint = GuardController.All.Count > 1 ? ", G: next guard" : "";
        return guard.name + " (Tab" + switchHint + ")   " + doing;
    }
}
#endif
