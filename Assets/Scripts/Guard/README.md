# Guard system (Person 3, Daniel)

Two guards for `PrisonLevel`:

- **Guard** patrols the hallway and the cell block lobby.
- **Guard_Room** stands in the guard room with the player, watching the monitor wall.

What they do:

- **Caught = restart.** If a guard has line of sight to the player while the alarm's wire puzzle is open, the level restarts (`CaughtRestart`).
- **Thrown rubbish.** Press **E** on the guard-room bin to take a piece, left-click to throw it. The room guard walks to where it lands, stands there puzzled for 8 seconds, then goes back.
- **Alarm.** When the alarm computer raises `onSolved`, both guards run to the distraction room and wait 15 seconds. If it is raised again while they wait, the 15 seconds start over.
- **Doors.** Guards open the guard-room door and the distraction-room door as they reach them and shut them behind them.
- **Collision.** The player cannot walk through a guard, and a guard stops and waits if the player stands in his way.

## Setting it up

Everything in the scene lives under one object called **Guards**. To build or repair it, open `PrisonLevel` and run **Tools > Guard > Build Guard In Open Scene**. It is safe to run again; if the scene ever conflicts in git, take the other version and run the tool.

The guard model is a `.glb`, which needs Unity's glTFast package. It is in `Packages/manifest.json`, so Unity downloads it the first time you open the project on this branch.

## Watching the guards (editor only)

In Play mode press **Tab** for a top-down guard camera (corner picture, then full screen, then off) and **G** to switch guard. To trigger things without playing, right-click the **Guard Controller** title in the Inspector: *Test: Distract Now*, *Test: Make Noise 3 m Behind Him*, *Test: Reset Guard*.

## Hooks for sound and UI

Each moment has an event in the Inspector on the object it happens to, and most also have a code event that fires for every guard or door at once.

| Moment | Inspector event | Code event |
|---|---|---|
| A guard hears a noise and starts walking over | `GuardController.onHeardNoise` | `GuardController.AnyGuardHeardNoise` |
| He reaches it and starts scratching his head | `GuardController.onPuzzled` | `GuardController.AnyGuardPuzzled` |
| A guard catches the player | `GuardController.onPrisonerSpotted`, `CaughtRestart.onCaught` | `GuardController.AnyGuardSpotted` |
| The alarm sends the guards off | `GuardAlarmLink.onAlarmSounded` (on the Guards object) | |
| A guard sets off, arrives in the distraction room, is back | `GuardController.onLeftPost`, `onReachedDistraction`, `onReturnedToPost` | |
| A guard's door starts to open, has shut | `GuardDoor.onOpened`, `onShut` | `GuardDoor.AnyDoorOpened`, `AnyDoorShut` |
| A guard's foot comes down | `GuardLimbAnimator.onFootstep` | `GuardLimbAnimator.AnyFootstep` |
| The player takes rubbish, throws it | `PlayerThrow.onPickedUp`, `onThrown` (on the Player) | |
| Thrown rubbish lands | | `ThrownNoise.AnyLanded` (gives the point) |

From code, subscribe in `OnEnable` and unsubscribe in `OnDisable`:

```csharp
void OnEnable()  { GuardController.AnyGuardPuzzled += PlayConfused; }
void OnDisable() { GuardController.AnyGuardPuzzled -= PlayConfused; }
void PlayConfused(GuardController guard) { AudioSource.PlayClipAtPoint(confusedClip, guard.transform.position); }
```

## What this touches outside its own files

- **Player object:** three components added (`GuardDetectable`, `PlayerSuspicion`, `PlayerThrow`) and its start position moved into the guard room.
- **`CCTV Monitors` group:** moved back onto the monitor wall, because it was left outside the room when the room was resized.
- Nothing else. The doors, the alarm computer, the bin and the distraction-room marker are only pointed at, not changed.

## Not done yet

- **Sounding the alarm again.** The guards are ready for it, but `AlarmComputerInteractable` ignores a press once it is solved. It needs to raise `onSolved` again.
- **Restart in a built game.** `CaughtRestart` reloads the scene, which works in the editor. A build also needs `PrisonLevel` in the Build Settings scene list.
- **Other puzzles.** To make another action count as suspicious, call `MarkSuspicious(seconds)` on the player's `PlayerSuspicion`.
