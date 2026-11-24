using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// UI for the prisoner cell-door keypad. Forwards button presses to
/// <see cref="CellDoorPuzzleLogic"/> and shows the entry and status messages.
///
/// The keypad is usable immediately and never displays the correct code; the clue
/// lives elsewhere in the 3D room and is independent of this class.
///
/// 3D-game integration: subscribe to <see cref="PuzzleCompleted"/>, hook up
/// <see cref="onPuzzleCompleted"/> in the Inspector, or poll <see cref="IsSolved"/>.
/// </summary>
public class CellDoorPuzzleController : MonoBehaviour
{
    [Header("Puzzle Setup")]
    [Tooltip("Correct code (digits only). Test default: 7394.")]
    [SerializeField] private string correctCode = "7394";
    [Tooltip("How many digits the keypad accepts. Should match the code's length.")]
    [SerializeField, Min(1)] private int codeLength = 4;

    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text displayText;
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Buttons for digits 0-9: element N is the button for digit N.")]
    [SerializeField] private Button[] digitButtons = new Button[10];
    [SerializeField] private Button enterButton;
    [SerializeField] private Button clearButton;

    [Header("Appearance")]
    [Tooltip("Show '_' for digits not typed yet (reveals only the code length).")]
    [SerializeField] private bool showPlaceholders = true;
    [SerializeField] private Color normalTextColor = new Color(0.75f, 0.85f, 0.75f);
    [SerializeField] private Color errorTextColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color successTextColor = new Color(0.4f, 1f, 0.5f);

    [Header("Messages")]
    [SerializeField] private string titleMessage = "CELL DOOR CONTROL";
    [SerializeField] private string promptMessage = "ENTER ACCESS CODE";
    [SerializeField] private string incorrectMessage = "INCORRECT CODE.";
    [SerializeField] private string successMessage = "CELL DOOR UNLOCKED.";

    [Header("Events")]
    [Tooltip("Fired once when the correct code is entered.")]
    public UnityEvent onPuzzleCompleted;

    /// <summary>C# event equivalent of onPuzzleCompleted for code subscribers.</summary>
    public event Action PuzzleCompleted;

    private CellDoorPuzzleLogic _logic;

    public bool IsSolved => _logic != null && _logic.IsSolved;

    private void Awake()
    {
        _logic = new CellDoorPuzzleLogic(correctCode, codeLength);
        _logic.EntryChanged += RefreshDisplay;
        _logic.Submitted += OnSubmitted;
        _logic.Completed += OnCompleted;

        // Each digit button forwards its own digit; any new input also resets the status line.
        for (int i = 0; i < digitButtons.Length; i++)
        {
            if (digitButtons[i] == null) continue;
            int digit = i;
            digitButtons[i].onClick.AddListener(() =>
            {
                if (_logic.PressDigit(digit)) SetStatus(promptMessage, normalTextColor);
            });
        }
        if (enterButton != null) enterButton.onClick.AddListener(() => _logic.Submit());
        if (clearButton != null) clearButton.onClick.AddListener(() =>
        {
            _logic.Clear();
            SetStatus(promptMessage, normalTextColor);
        });

        if (titleText != null) titleText.text = titleMessage;
        RefreshDisplay();
        SetStatus(promptMessage, normalTextColor);
    }

    /// <summary>
    /// Changes the correct code at runtime (e.g. when the 3D game generates it) and
    /// restarts the keypad. Digits only.
    /// </summary>
    public void SetCode(string code)
    {
        correctCode = code;
        codeLength = code.Length;
        _logic.Configure(correctCode, codeLength);
        SetKeypadInteractable(true);
        SetStatus(promptMessage, normalTextColor);
    }

    private void OnSubmitted(CellDoorSubmitResult result)
    {
        // On a wrong code the logic has already cleared the entry; just report it.
        if (result == CellDoorSubmitResult.Incorrect) SetStatus(incorrectMessage, errorTextColor);
    }

    private void OnCompleted()
    {
        SetStatus(successMessage, successTextColor);
        SetKeypadInteractable(false); // lock the keypad after solving
        PuzzleCompleted?.Invoke();
        onPuzzleCompleted?.Invoke();
    }

    private void SetKeypadInteractable(bool value)
    {
        foreach (var b in digitButtons) if (b != null) b.interactable = value;
        if (enterButton != null) enterButton.interactable = value;
        if (clearButton != null) clearButton.interactable = value;
    }

    private void RefreshDisplay()
    {
        if (displayText == null) return;
        var sb = new StringBuilder(_logic.Entry);
        if (showPlaceholders)
            for (int i = sb.Length; i < _logic.MaxLength; i++) sb.Append('_');
        // Spaced out so it reads like a keypad readout.
        displayText.text = string.Join(" ", sb.ToString().ToCharArray());
    }

    private void SetStatus(string message, Color color)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = color;
    }

    // Keep the Inspector values consistent (digits only, length matches the code).
    private void OnValidate()
    {
        var digits = new StringBuilder();
        foreach (char c in correctCode ?? "") if (c >= '0' && c <= '9') digits.Append(c);
        correctCode = digits.Length > 0 ? digits.ToString() : "0000";
        if (codeLength != correctCode.Length)
            Debug.LogWarning("[CellDoorPuzzle] Code length (" + codeLength + ") doesn't match the code's digit count (" + correctCode.Length + "). The keypad accepts min of the two.", this);
    }
}
