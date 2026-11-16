using System;
using System.Text;

/// <summary>Outcome of pressing Enter on the keypad.</summary>
public enum CellDoorSubmitResult
{
    Correct,   // code matched, door unlocked
    Incorrect, // wrong (or incomplete) code, entry was cleared
    Ignored    // nothing entered, or the keypad is already unlocked
}

/// <summary>
/// Pure keypad rules with no Unity/UI dependencies. The correct code lives here
/// only; nothing in this class exposes it to the UI.
/// </summary>
public class CellDoorPuzzleLogic
{
    private readonly StringBuilder _entry = new StringBuilder();
    private string _code;
    private int _maxLength;

    public bool IsSolved { get; private set; }

    /// <summary>Digits typed so far.</summary>
    public string Entry => _entry.ToString();

    public int MaxLength => _maxLength;

    public event Action EntryChanged;                    // digits added or cleared
    public event Action<CellDoorSubmitResult> Submitted; // Enter was pressed
    public event Action Completed;                       // correct code entered

    public CellDoorPuzzleLogic(string code, int codeLength)
    {
        Configure(code, codeLength);
    }

    /// <summary>Sets the correct code and the number of digits the keypad accepts.</summary>
    public void Configure(string code, int codeLength)
    {
        if (string.IsNullOrEmpty(code)) throw new ArgumentException("Code must not be empty.", nameof(code));
        foreach (char c in code)
            if (c < '0' || c > '9') throw new ArgumentException("Code must contain digits only.", nameof(code));

        _code = code;
        // The keypad never accepts more digits than the code has.
        _maxLength = Math.Max(1, Math.Min(codeLength, code.Length));
        _entry.Clear();
        IsSolved = false;
        EntryChanged?.Invoke();
    }

    /// <summary>Adds a digit (0-9) to the entry. Ignored when full or already solved.</summary>
    public bool PressDigit(int digit)
    {
        if (IsSolved || digit < 0 || digit > 9 || _entry.Length >= _maxLength) return false;
        _entry.Append((char)('0' + digit));
        EntryChanged?.Invoke();
        return true;
    }

    /// <summary>Clears the digits typed so far.</summary>
    public void Clear()
    {
        if (IsSolved || _entry.Length == 0) return;
        _entry.Clear();
        EntryChanged?.Invoke();
    }

    /// <summary>Checks the entry against the code. A wrong or short entry is cleared so the player can retry.</summary>
    public CellDoorSubmitResult Submit()
    {
        if (IsSolved || _entry.Length == 0) return CellDoorSubmitResult.Ignored;

        bool correct = _entry.ToString() == _code;
        CellDoorSubmitResult result;
        if (correct)
        {
            IsSolved = true;
            result = CellDoorSubmitResult.Correct;
        }
        else
        {
            _entry.Clear();
            result = CellDoorSubmitResult.Incorrect;
        }

        EntryChanged?.Invoke();
        Submitted?.Invoke(result);
        if (correct) Completed?.Invoke();
        return result;
    }
}
