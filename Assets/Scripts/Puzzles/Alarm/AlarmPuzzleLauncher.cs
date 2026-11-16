using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens the AlarmPuzzle scene on top of the running 3D scene (additively) and
/// closes it again. This is the single entry point the 3D game should use:
///
///     AlarmPuzzleLauncher.Open(onSolved: () => TriggerAlarm());
///
/// While the puzzle is open the cursor is freed. Subscribe to <see cref="Opened"/>
/// and <see cref="Closed"/> to disable / re-enable player movement and camera look.
/// </summary>
public static class AlarmPuzzleLauncher
{
    public const string SceneName = "AlarmPuzzle";

    /// <summary>Raised when the puzzle window has opened (pause player input here).</summary>
    public static event Action Opened;
    /// <summary>Raised after the window closes, solved or not (restore player input here).</summary>
    public static event Action Closed;

    public static bool IsOpen { get; private set; }

    private static Runner _runner;

    // If "Enter Play Mode without domain reload" is on, statics survive between play sessions,
    // so clear them at startup or a stale IsOpen would block the puzzle from opening.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsOpen = false;
        _runner = null;
        Opened = null;
        Closed = null;
    }

    /// <summary>
    /// Opens the puzzle.
    /// </summary>
    /// <param name="onSolved">Called when the puzzle is solved, as the window closes.</param>
    /// <param name="onClosed">Called when the window closes for any reason.</param>
    /// <param name="order">Optional cut order overriding the one set in the Inspector.</param>
    public static void Open(Action onSolved = null, Action onClosed = null, IList<WireColor> order = null)
    {
        if (IsOpen) return; // ignore repeat calls while already open
        IsOpen = true;

        if (_runner == null)
        {
            var go = new GameObject("AlarmPuzzleLauncher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }
        _runner.StartCoroutine(_runner.Run(onSolved, onClosed, order));
    }

    /// <summary>Closes the puzzle without solving it (also bound to Esc and the Close button).</summary>
    public static void Close()
    {
        if (_runner != null) _runner.RequestClose();
    }

    // MonoBehaviour host so we can run coroutines and poll Esc.
    private class Runner : MonoBehaviour
    {
        private bool _closeRequested;

        public void RequestClose() => _closeRequested = true;

        private void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                _closeRequested = true;
        }

        public IEnumerator Run(Action onSolved, Action onClosed, IList<WireColor> order)
        {
            _closeRequested = false;

            var load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            if (load == null)
            {
                Debug.LogError("[AlarmPuzzle] Could not load scene '" + SceneName + "'. Is it in Build Settings?");
                IsOpen = false;
                yield break;
            }
            yield return load;

            var scene = SceneManager.GetSceneByName(SceneName);
            var controller = FindController(scene);
            if (controller == null)
            {
                Debug.LogError("[AlarmPuzzle] No AlarmPuzzleController found in the puzzle scene.");
                yield return SceneManager.UnloadSceneAsync(scene);
                IsOpen = false;
                yield break;
            }

            SilenceDuplicates(scene);
            if (order != null) controller.SetOrder(order);

            bool solved = false;
            controller.PuzzleCompleted += () => solved = true;
            controller.CloseRequested += RequestClose;

            // Free the cursor so the player can click wires.
            var prevLock = Cursor.lockState;
            var prevVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Opened?.Invoke();

            // Wait until solved or closed. After a solve, linger so "Alarm activated." can be read.
            while (!solved && !_closeRequested) yield return null;
            if (solved) yield return new WaitForSecondsRealtime(1.5f);

            yield return SceneManager.UnloadSceneAsync(scene);
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
            IsOpen = false;

            if (solved) onSolved?.Invoke();
            onClosed?.Invoke();
            Closed?.Invoke();
        }

        private static AlarmPuzzleController FindController(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<AlarmPuzzleController>(true);
                if (c != null) return c;
            }
            return null;
        }

        // The puzzle scene has its own camera, light and EventSystem so it works standalone.
        // When layered over the 3D scene those would duplicate the game's, so switch them off.
        private static void SilenceDuplicates(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                foreach (var l in root.GetComponentsInChildren<AudioListener>(true)) l.enabled = false;
                foreach (var l in root.GetComponentsInChildren<Light>(true)) l.enabled = false;
                // Keep the puzzle's EventSystem only if the 3D scene has none (UI needs exactly one).
                foreach (var es in root.GetComponentsInChildren<EventSystem>(true))
                {
                    foreach (var other in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
                    {
                        if (other.gameObject.scene != scene) { es.gameObject.SetActive(false); break; }
                    }
                }
            }
        }
    }
}
