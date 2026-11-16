using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UI presentation for the guard-room alarm puzzle. Owns the Canvas widgets and
/// forwards clicks to <see cref="AlarmPuzzleLogic"/>.
///
/// 3D-game integration: subscribe to <see cref="PuzzleCompleted"/> (C# event),
/// hook up <see cref="onPuzzleCompleted"/> (UnityEvent in the Inspector), or
/// poll <see cref="IsSolved"/>.
/// </summary>
public class AlarmPuzzleController : MonoBehaviour
{
    [Serializable]
    public class WireView
    {
        public WireColor color;
        public Button button;
        [Tooltip("Image tinted to show active vs. cut.")]
        public Image image;
        [Tooltip("Optional label that gets a 'CUT' marker.")]
        public TMP_Text label;
        [Tooltip("Optional object (e.g. an 'X') shown only once cut.")]
        public GameObject cutMarker;
        [Tooltip("Colour of the wire while it is still uncut.")]
        public Color activeColor = Color.white;
    }

    [Header("Puzzle Setup")]
    [Tooltip("Required cut order. Default: Blue, Red, Yellow.")]
    [SerializeField] private List<WireColor> cutOrder = new List<WireColor> { WireColor.Red, WireColor.Yellow, WireColor.Blue,};

    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button resetButton;
    [Tooltip("Optional. Raises CloseRequested so a launcher can back out of the puzzle.")]
    [SerializeField] private Button closeButton;
    [SerializeField] private List<WireView> wires = new List<WireView>();

    [Header("Appearance")]
    [SerializeField] private Color cutColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color failureTextColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color successTextColor = new Color(0.4f, 1f, 0.5f);
    [SerializeField] private Color normalTextColor = Color.white;

    [Header("Messages")]
    [SerializeField] private string titleMessage = "ALARM CONTROL - Cut the wires in the correct order";
    [SerializeField] private string readyMessage = "Select a wire to cut.";
    [SerializeField] private string correctMessage = "Wire cut. Continue...";
    [SerializeField] private string failureMessage = "Wrong wire! System reset. Start over.";
    [SerializeField] private string successMessage = "Alarm activated.";

    [Header("Events")]
    [Tooltip("Fired once when all wires are cut correctly.")]
    public UnityEvent onPuzzleCompleted;

    /// <summary>C# event equivalent of onPuzzleCompleted for code subscribers.</summary>
    public event Action PuzzleCompleted;

    /// <summary>Raised when the player presses the Close button.</summary>
    public event Action CloseRequested;

    private AlarmPuzzleLogic _logic;

    public bool IsSolved => _logic != null && _logic.IsSolved;

    private void Awake()
    {
        BuildLogic();

        // Hook each wire button to its colour.
        foreach (var wire in wires)
        {
            if (wire.button == null) continue;
            var captured = wire.color;
            wire.button.onClick.AddListener(() => _logic.TryCut(captured));
        }
        if (resetButton != null) resetButton.onClick.AddListener(ResetPuzzle);
        if (closeButton != null) closeButton.onClick.AddListener(() => CloseRequested?.Invoke());

        if (titleText != null) titleText.text = titleMessage;
        RefreshAllWires();
        SetStatus(readyMessage, normalTextColor);
    }

    /// <summary>Creates the logic object from the current cut order and subscribes to it.</summary>
    private void BuildLogic()
    {
        _logic = new AlarmPuzzleLogic(cutOrder);
        _logic.WireCut += OnWireCut;
        _logic.WrongWire += OnWrongWire;
        _logic.Completed += OnCompleted;
        _logic.Reset += RefreshAllWires;
    }

    /// <summary>
    /// Replaces the required cut order at runtime (e.g. the order the player found
    /// elsewhere in the game) and restarts the puzzle. Wires must be unique.
    /// </summary>
    public void SetOrder(IList<WireColor> order)
    {
        cutOrder = new List<WireColor>(order);
        BuildLogic();
        RefreshAllWires();
        SetStatus(readyMessage, normalTextColor);
    }

    /// <summary>Resets the puzzle (also used by the Reset button).</summary>
    public void ResetPuzzle()
    {
        _logic.ResetPuzzle();
        SetStatus(readyMessage, normalTextColor);
    }

    private void OnWireCut(WireColor wire)
    {
        RefreshWire(wire);
        // If this was the last wire, Completed fires next and overrides the message.
        SetStatus(correctMessage, normalTextColor);
    }

    private void OnWrongWire(WireColor wire)
    {
        // The logic resets itself right after this event (which only refreshes wires),
        // so the failure message stays visible.
        SetStatus(failureMessage, failureTextColor);
    }

    private void OnCompleted()
    {
        SetStatus(successMessage, successTextColor);
        // Lock input: the logic ignores clicks once solved; also disable the buttons.
        foreach (var w in wires) if (w.button != null) w.button.interactable = false;
        PuzzleCompleted?.Invoke();
        onPuzzleCompleted?.Invoke();
    }

    private void RefreshAllWires()
    {
        foreach (var w in wires) RefreshView(w);
    }

    private void RefreshWire(WireColor color)
    {
        foreach (var w in wires) if (w.color == color) RefreshView(w);
    }

    private void RefreshView(WireView w)
    {
        bool cut = _logic.IsCut(w.color);
        if (w.button != null) w.button.interactable = !cut; // cut wires can't be clicked again
        if (w.image != null) w.image.color = cut ? cutColor : w.activeColor;
        if (w.cutMarker != null) w.cutMarker.SetActive(cut);
        if (w.label != null) w.label.text = cut ? w.color.ToString().ToUpper() + " (CUT)" : w.color.ToString().ToUpper();
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = color;
    }
}
