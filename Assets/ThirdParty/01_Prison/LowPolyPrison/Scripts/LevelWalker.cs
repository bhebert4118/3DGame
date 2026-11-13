#define MACHINES
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM_PACKAGE  // "Both" defines the first without the package
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace HeyHeyThere.LowPolyPrison
{
    /// <summary>
    /// The demo level's player: WASD or the arrows and the mouse, Shift runs, E opens what is in
    /// front (its clip plays to half way and waits there, open; E again plays the rest, shut), Esc
    /// frees the mouse and a click takes it back. H, or the Help button while the mouse is free, says
    /// the goal and the keys again. Either input handling works.
#if MACHINES
    /// E uses a Machine in front: the part the view is on, taking what it needs from the items carried.
#endif
#if PICKUPS
    /// E takes a Pickup in front into the items carried and holds it up in the Inspect view, saves at a
    /// save point and opens storage.
#endif
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class LevelWalker : MonoBehaviour
    {
        public Transform view;
        public float walk = 2.2f, run = 4f, mouse = 2f, reach = 2f;
        [TextArea] public string goal = "";  // shown at the start with the keys

        CharacterController body;
        Vector3 start;
        float pitch, fall;
        readonly List<AnimationState> opening = new List<AnimationState>();
        string note = "";
        float noteLeft;
        string narration = "";  // the goal and keys said at the start, H says them again
        static readonly Rect HelpButton = new Rect(20f, 20f, 70f, 28f);
#if MACHINES || PICKUPS
        public List<string> items = new List<string>();
        const string Keys = "WASD and the mouse to walk, E to use or take, Shift to run, Esc to free the mouse, H for help";
#else
        const string Keys = "WASD and the mouse to walk, E to open or shut, Shift to run, Esc to free the mouse, H for help";
#endif
#if PICKUPS
        public Inspect inspect;
#endif

#if UNITY_6000_6_OR_NEWER  // the sort mode is obsolete from 6.6, the overload without it new
        static T[] All<T>() where T : UnityEngine.Object => FindObjectsByType<T>(FindObjectsInactive.Include);
#else
        static T[] All<T>() where T : UnityEngine.Object => FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif

        void Start()
        {
            body = GetComponent<CharacterController>();
            start = transform.position;
            Cursor.lockState = CursorLockMode.Locked;
            narration = (goal == "" ? "" : goal + "\n") + Keys;
            Say(narration, 8f);
#if MACHINES
            foreach (var m in All<Machine>())
            {
                m.onRefused.AddListener(why => Say(why == "power" ? "No power" : why == "code" ? "Wrong code" : $"It needs the {why.Replace('_', ' ')}"));
                m.onPicked.AddListener(it => Say($"Picked up the {it.Replace('_', ' ')}"));
            }
#endif
#if PICKUPS
            foreach (var p in All<Pickup>())
            {
                var at = p;
                p.onRefused.AddListener(why => Say($"It needs the {why.Replace('_', ' ')}"));
                p.onPicked.AddListener(it => Say($"Picked up the {(at.title != "" ? at.title.ToLowerInvariant() : it.Replace('_', ' '))}"));
                p.onSaved.AddListener(() => Say("The game is saved" + (at.takes != "" ? $" ({items.FindAll(i => i == at.takes).Count} {at.takes.Replace('_', ' ')} left)" : "")));
            }
#endif
        }

        void Update()
        {
#if PICKUPS
            if (inspect != null && inspect.Showing)
                return;
#endif
            if (Free())
                Cursor.lockState = CursorLockMode.None;
            else if (Click() && !(Cursor.lockState != CursorLockMode.Locked && OnHelp()))
                Cursor.lockState = CursorLockMode.Locked;
            if (Help())
                Say(narration, 8f);
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var d = Look() * mouse;
                transform.Rotate(0f, d.x, 0f);
                pitch = Mathf.Clamp(pitch - d.y, -80f, 80f);
                view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            var m = Vector2.ClampMagnitude(Move(), 1f) * (Running() ? run : walk);
            fall = body.isGrounded ? -1f : fall - 9.8f * Time.deltaTime;
            body.Move((transform.right * m.x + transform.forward * m.y + Vector3.up * fall) * Time.deltaTime);
            if (transform.position.y < -20f)
            {
                body.enabled = false;
                transform.position = start;
                body.enabled = true;
            }
            if (Use())
                Open();
            for (var i = opening.Count - 1; i >= 0; i--)
                if (opening[i].time >= opening[i].length / 2f)
                {
                    opening[i].speed = 0f;
                    opening.RemoveAt(i);
                }
        }

        void Open()
        {
            if (!Physics.Raycast(view.position, view.forward, out var hit, reach))
                return;
#if PICKUPS
            var pickup = hit.collider.GetComponentInParent<Pickup>();
            if (pickup != null)
            {
                if (pickup.Use(items, hit.collider.name) && pickup.kind == Pickup.Kind.Item && inspect != null)
                    inspect.Open(pickup.gameObject);
                return;
            }
#endif
#if MACHINES
            var machine = hit.collider.GetComponentInParent<Machine>();
            if (machine != null)
            {
                machine.Use(items, hit.collider.name);
                return;
            }
#endif
            var anim = hit.collider.GetComponentInParent<Animation>();
            if (anim == null)
                return;
            foreach (AnimationState state in anim)
            {
                if (state.wrapMode == WrapMode.Loop)
                {
                    if (anim.isPlaying) anim.Stop(); else anim.Play(state.name);
                }
                else if (state.enabled && state.speed == 0f)
                    state.speed = 1f;
                else if (!anim.isPlaying)
                {
                    anim.Play(state.name);
                    state.speed = 1f;
                    opening.Add(state);
                }
                return;
            }
        }

        void Say(string text, float seconds = 2.5f)
        {
            note = text;
            noteLeft = seconds;
        }

        void OnGUI()
        {
#if PICKUPS
            if (inspect != null && inspect.Showing)
                return;
#endif
            GUI.Label(new Rect(Screen.width / 2f - 4f, Screen.height / 2f - 10f, 20f, 20f), "+");
            if (Cursor.lockState != CursorLockMode.Locked && GUI.Button(HelpButton, "Help"))
                Say(narration, 8f);
            noteLeft -= Time.unscaledDeltaTime;
            if (noteLeft > 0f)
            {
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true };
                var width = Mathf.Min(640f, Screen.width - 40f);
                GUI.Label(new Rect((Screen.width - width) / 2f, Screen.height * 0.75f, width, style.CalcHeight(new GUIContent(note), width)), note, style);
            }
#if MACHINES || PICKUPS
            var carried = items.GroupBy(it => it).Select(g => g.Count() > 1 ? $"{g.Key} x{g.Count()}" : g.Key).ToList();
            GUI.Label(new Rect(Screen.width - 220f, Screen.height - 30f - 20f * carried.Count, 200f, 20f * carried.Count + 10f),
                      string.Join("\n", carried).Replace('_', ' '));
#endif
        }

#if ENABLE_INPUT_SYSTEM && INPUT_SYSTEM_PACKAGE
        static Vector2 Move()
        {
            var k = Keyboard.current;
            return k == null ? Vector2.zero : new Vector2(Key(k.dKey, k.rightArrowKey) - Key(k.aKey, k.leftArrowKey),
                                                          Key(k.wKey, k.upArrowKey) - Key(k.sKey, k.downArrowKey));
        }

        static float Key(KeyControl a, KeyControl b) => a.isPressed || b.isPressed ? 1f : 0f;
        static bool Running() => Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        static bool Use() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        static bool Free() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        static bool Click() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        static bool Help() => Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
        static Vector2 Pointer() => Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
        static Vector2 Look() => Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * 0.1f; // as the old Mouse X and Y
#else
        static Vector2 Move() => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        static bool Running() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        static bool Use() => Input.GetKeyDown(KeyCode.E);
        static bool Free() => Input.GetKeyDown(KeyCode.Escape);
        static bool Click() => Input.GetMouseButtonDown(0);
        static bool Help() => Input.GetKeyDown(KeyCode.H);
        static Vector2 Pointer() => Input.mousePosition;
        static Vector2 Look() => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
#endif

        // the click on Help leaves the mouse free; the screen's y is up, the GUI's down
        static bool OnHelp() => HelpButton.Contains(new Vector2(Pointer().x, Screen.height - Pointer().y));
    }
}
