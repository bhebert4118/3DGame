using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Editor helper that (re)builds the cell-door keypad UI in
/// Assets/Scenes/Puzzles/CellDoorPuzzle.unity. Menu: Tools > Puzzles > Build Cell Door Puzzle UI.
/// Safe to re-run: it rebuilds the keypad panel and reuses the canvas/EventSystem.
/// </summary>
public static class CellDoorPuzzleSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Puzzles/CellDoorPuzzle.unity";
    const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

    static readonly Color Steel = new Color(0.17f, 0.18f, 0.20f);
    static readonly Color KeyColor = new Color(0.36f, 0.38f, 0.41f);
    static readonly Color ScreenBg = new Color(0.03f, 0.07f, 0.04f);

    [MenuItem("Tools/Puzzles/Build Cell Door Puzzle UI")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Canvas (Screen Space - Overlay)
        var canvasGo = GameObject.Find("PuzzleWindow");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("PuzzleWindow", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
        }
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var old = canvasGo.transform.Find("KeypadPanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var oldCtrl = canvasGo.GetComponent<CellDoorPuzzleController>();
        if (oldCtrl != null) Object.DestroyImmediate(oldCtrl);

        // Steel housing
        var panel = NewUI("KeypadPanel", canvasGo.transform);
        panel.gameObject.AddComponent<Image>().color = Steel;
        Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(580, 940), Vector2.zero);

        var title = NewText("TitleText", panel, "", 38, TextAlignmentOptions.Center);
        title.color = new Color(0.85f, 0.87f, 0.9f);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520, 70), new Vector2(0, -30));

        // Recessed display "screen"
        var screen = NewUI("DisplayScreen", panel);
        screen.gameObject.AddComponent<Image>().color = ScreenBg;
        Anchor(screen, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(480, 120), new Vector2(0, -115));
        var display = NewText("DisplayText", screen, "", 64, TextAlignmentOptions.Center);
        display.color = new Color(0.45f, 1f, 0.55f);
        Fill(display.rectTransform);

        var status = NewText("StatusText", panel, "", 28, TextAlignmentOptions.Center);
        Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(520, 50), new Vector2(0, -255));

        // Keys: 1-9, then Clear / 0 / Enter. Row spacing 125, column spacing 160.
        var digitButtons = new Button[10];
        int[,] layout = { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 }, { -1, 0, -2 } }; // -1 = Clear, -2 = Enter
        Button enter = null, clear = null;
        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                int k = layout[r, c];
                string label = k >= 0 ? k.ToString() : (k == -1 ? "CLEAR" : "ENTER");
                Color col = k >= 0 ? KeyColor : (k == -1 ? new Color(0.65f, 0.3f, 0.15f) : new Color(0.15f, 0.5f, 0.25f));
                var btn = NewKey(k >= 0 ? "Key" + k : (k == -1 ? "ClearButton" : "EnterButton"), panel, label, col,
                    new Vector2((c - 1) * 160, -330 - r * 125), k >= 0 ? 52 : 30);
                if (k >= 0) digitButtons[k] = btn;
                else if (k == -1) clear = btn;
                else enter = btn;
            }
        }

        // Controller + references
        var ctrl = canvasGo.AddComponent<CellDoorPuzzleController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("displayText").objectReferenceValue = display;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("enterButton").objectReferenceValue = enter;
        so.FindProperty("clearButton").objectReferenceValue = clear;
        var digitsProp = so.FindProperty("digitButtons");
        digitsProp.arraySize = 10;
        for (int i = 0; i < 10; i++) digitsProp.GetArrayElementAtIndex(i).objectReferenceValue = digitButtons[i];
        so.ApplyModifiedProperties();

        EnsureEventSystem();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // Adds an EventSystem wired to the project's InputSystem_Actions asset if the scene has none.
    static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem", typeof(EventSystem));
        var module = go.AddComponent<InputSystemUIInputModule>();
        var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        var refs = AssetDatabase.LoadAllAssetsAtPath(ActionsPath).OfType<InputActionReference>().ToList();
        InputActionReference R(string n) => refs.FirstOrDefault(x => x.name == n);

        module.actionsAsset = asset;
        module.point = R("UI/Point");
        module.move = R("UI/Navigate");
        module.submit = R("UI/Submit");
        module.cancel = R("UI/Cancel");
        module.leftClick = R("UI/Click");
        module.middleClick = R("UI/MiddleClick");
        module.rightClick = R("UI/RightClick");
        module.scrollWheel = R("UI/ScrollWheel");
        module.trackedDevicePosition = R("UI/TrackedDevicePosition");
        module.trackedDeviceOrientation = R("UI/TrackedDeviceOrientation");
    }

    static Button NewKey(string name, Transform parent, string label, Color color, Vector2 pos, float fontSize)
    {
        var rt = NewUI(name, parent);
        Anchor(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(140, 100), pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var t = NewText("Label", rt, label, fontSize, TextAlignmentOptions.Center);
        t.raycastTarget = false;
        Fill(t.rectTransform);
        return btn;
    }

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.alignment = align; t.color = Color.white;
        return t;
    }

    // Anchor to a point, with pivot at the same point, fixed size and offset.
    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
