using System;
using System.Collections.Generic;

/// <summary>The wires available in the alarm puzzle.</summary>
public enum WireColor
{
    Blue,
    Red,
    Yellow
}

/// <summary>Outcome of a single wire cut.</summary>
public enum CutResult
{
    Correct,   // right wire, more wires remain
    Wrong,     // wrong wire, puzzle was reset
    Completed, // right wire and it was the last one
    Ignored    // input ignored (already solved, or wire already cut)
}

/// <summary>
/// Pure puzzle logic with no Unity/UI dependencies, so it is easy to test
/// and reuse. The UI (AlarmPuzzleController) just forwards clicks here.
/// </summary>
public class AlarmPuzzleLogic
{
    private readonly List<WireColor> _order;
    private readonly HashSet<WireColor> _cut = new HashSet<WireColor>();
    private int _step;

    public bool IsSolved { get; private set; }
    public int Step => _step;
    public int TotalSteps => _order.Count;

    /// <summary>The wire the player must cut next (null when solved).</summary>
    public WireColor? ExpectedWire => IsSolved ? (WireColor?)null : _order[_step];

    public event Action<WireColor> WireCut;      // a correct cut
    public event Action<WireColor> WrongWire;    // fired just before the reset
    public event Action Completed;               // all wires cut correctly
    public event Action Reset;                   // puzzle returned to start

    public AlarmPuzzleLogic(IList<WireColor> order)
    {
        if (order == null || order.Count == 0)
            throw new ArgumentException("Cut order must contain at least one wire.", nameof(order));
        _order = new List<WireColor>(order);
    }

    public bool IsCut(WireColor wire) => _cut.Contains(wire);

    public CutResult TryCut(WireColor wire)
    {
        if (IsSolved || _cut.Contains(wire)) return CutResult.Ignored;

        if (wire != _order[_step])
        {
            WrongWire?.Invoke(wire);
            ResetPuzzle();
            return CutResult.Wrong;
        }

        _cut.Add(wire);
        _step++;
        WireCut?.Invoke(wire);

        if (_step >= _order.Count)
        {
            IsSolved = true;
            Completed?.Invoke();
            return CutResult.Completed;
        }
        return CutResult.Correct;
    }

    /// <summary>Restores all wires and rewinds to the first required wire.</summary>
    public void ResetPuzzle()
    {
        _cut.Clear();
        _step = 0;
        IsSolved = false;
        Reset?.Invoke();
    }
}
