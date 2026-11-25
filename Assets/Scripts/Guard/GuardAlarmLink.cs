using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Sends every guard to the distraction room when the alarm goes off.
///
/// It listens to AlarmComputerInteractable.onSolved from the guards' side while the game runs, so
/// nothing has to be wired up on the alarm computer object itself. Each time that event is raised,
/// every guard gets Distract(): the first time they run off, and a later one while they are waiting
/// in the distraction room tops their wait back up.
/// </summary>
public class GuardAlarmLink : MonoBehaviour
{
    [Tooltip("Raised every time the alarm sends the guards off (for the siren).")]
    public UnityEvent onAlarmSounded;

    private AlarmComputerInteractable[] _alarms = new AlarmComputerInteractable[0];

    private void OnEnable()
    {
        _alarms = FindObjectsByType<AlarmComputerInteractable>(FindObjectsInactive.Include);
        foreach (AlarmComputerInteractable alarm in _alarms)
        {
            if (alarm.onSolved == null) alarm.onSolved = new UnityEvent();
            alarm.onSolved.AddListener(DistractAllGuards);
        }
    }

    private void OnDisable()
    {
        foreach (AlarmComputerInteractable alarm in _alarms)
            if (alarm != null && alarm.onSolved != null) alarm.onSolved.RemoveListener(DistractAllGuards);
    }

    /// <summary>Sounds the alarm for the guards: all of them head for the distraction room.</summary>
    public void DistractAllGuards()
    {
        var guards = GuardController.All;
        for (int i = 0; i < guards.Count; i++) guards[i].Distract();
        onAlarmSounded?.Invoke();
    }
}
