using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// "Any suspicious interaction in a guard's line of sight restarts the level."
/// Listens for any guard spotting something (GuardController.AnyGuardSpotted), freezes the game,
/// shows CAUGHT for a moment, then reloads the scene this object is in.
///
/// This is a simple stand-in for the game manager's restart. To replace it, remove this object and
/// subscribe to GuardController.AnyGuardSpotted (or hook <see cref="onCaught"/>) instead.
/// </summary>
public class CaughtRestart : MonoBehaviour
{
    [Tooltip("Seconds the CAUGHT screen stays up before the level restarts.")]
    [SerializeField] private float delay = 2.5f;
    [SerializeField] private string title = "CAUGHT";
    [SerializeField] private string hint = "A guard saw you. Get him looking somewhere else first.";

    [Tooltip("Raised the moment a guard catches the player, before the restart.")]
    public UnityEvent onCaught;

    /// <summary>True from the moment of being caught until the level has reloaded.</summary>
    public bool IsCaught { get; private set; }

    private GUIStyle _titleStyle, _hintStyle;

    private void OnEnable() => GuardController.AnyGuardSpotted += OnSpotted;

    private void OnDisable() => GuardController.AnyGuardSpotted -= OnSpotted;

    private void OnSpotted(GuardController guard, Transform target)
    {
        if (IsCaught) return;
        IsCaught = true;
        onCaught?.Invoke();
        StartCoroutine(Restart());
    }

    private IEnumerator Restart()
    {
        AlarmPuzzleLauncher.Close(); // shut the wire puzzle if that is what he was caught at
        Time.timeScale = 0f;         // freeze guards and player under the CAUGHT screen

        yield return new WaitForSecondsRealtime(delay);
        while (AlarmPuzzleLauncher.IsOpen) yield return null; // let the puzzle finish closing first

        Time.timeScale = 1f;
        Scene scene = gameObject.scene;
#if UNITY_EDITOR
        // Works in the editor even when the scene is not in the Build Settings list.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else Debug.LogError("[CaughtRestart] '" + scene.name + "' is not in the Build Settings scene list, so it cannot be reloaded.");
#endif
    }

    private void OnGUI()
    {
        if (!IsCaught) return;

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 64, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(1f, 0.3f, 0.25f);
            _hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            _hintStyle.normal.textColor = Color.white;
        }

        GUI.depth = -100; // on top of the other on-screen prompts
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.5f - 90f, Screen.width, 90f), title, _titleStyle);
        GUI.Label(new Rect(0f, Screen.height * 0.5f + 10f, Screen.width, 40f), hint, _hintStyle);
    }
}
