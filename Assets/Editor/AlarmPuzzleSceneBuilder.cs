using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Editor helper that (re)builds the alarm puzzle UI inside the currently open scene.
/// Menu: Tools > Puzzles > Build Alarm Puzzle UI. Safe to re-run: it reuses the
/// existing PuzzleWindow canvas and EventSystem and only rebuilds the panel.
/// </summary>
public static class AlarmPuzzleSceneBuilder
{
    [MenuItem("Tools/Puzzles/Build Alarm Puzzle UI")]
    public static void Build()
    {
        // Canvas: reuse the scene's "PuzzleWindow" if present.
        var canvasGo = GameObject.Find("PuzzleWindow");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("PuzzleWindow", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
        }
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Remove an old panel and any old controller so the build is repeatable.
        var old = canvasGo.transform.Find("PuzzlePanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var oldCtrl = canvasGo.GetComponent<AlarmPuzzleController>();
        if (oldCtrl != null) Object.DestroyImmediate(oldCtrl);

        // Main panel
        var panel = NewUI("PuzzlePanel", canvasGo.transform);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.09f, 0.11f, 0.95f);
        Stretch(panel, new Vector2(0.5f, 0.5f), new Vector2(1000, 700), Vector2.zero);

        var title = NewText("TitleText", panel, "", 40, TextAlignmentOptions.Center);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(920, 120), new Vector2(0, -80));

        var colors = new (WireColor c, Color col)[]
        {
            (WireColor.Blue,   new Color(0.2f, 0.45f, 1f)),
            (WireColor.Red,    new Color(0.9f, 0.15f, 0.15f)),
            (WireColor.Yellow, new Color(1f, 0.85f, 0.1f)),
        };

        var views = new AlarmPuzzleController.WireView[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            // Wire = a Button whose Image is the coloured bar.
            var rt = NewUI(colors[i].c + "Wire", panel);
            Place(rt, new Vector2(0.5f, 1f), new Vector2(700, 80), new Vector2(0, -220 - i * 110));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colors[i].col;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            // Label on the wire (dark text readable on all three colours).
            var label = NewText("Label", rt, colors[i].c.ToString().ToUpper(), 34, TextAlignmentOptions.Center);
            label.color = Color.black;
            Fill(label.rectTransform);
            label.raycastTarget = false;

            // Cut marker: a dark gap in the middle of the bar, hidden until cut.
            var gap = NewUI("CutGap", rt);
            var gapImg = gap.gameObject.AddComponent<Image>();
            gapImg.color = new Color(0.08f, 0.09f, 0.11f, 1f);
            gapImg.raycastTarget = false;
            Place(gap, new Vector2(0.5f, 0.5f), new Vector2(50, 100), Vector2.zero);
            gap.gameObject.SetActive(false);

            views[i] = new AlarmPuzzleController.WireView
            {
                color = colors[i].c, button = btn, image = img, label = label,
                cutMarker = gap.gameObject, activeColor = colors[i].col
            };
        }

        var status = NewText("StatusText", panel, "", 34, TextAlignmentOptions.Center);
        Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(920, 80), new Vector2(0, 130));

        var resetRt = NewUI("ResetButton", panel);
        Place(resetRt, new Vector2(0.5f, 0f), new Vector2(240, 60), new Vector2(0, 50));
        var resetImg = resetRt.gameObject.AddComponent<Image>();
        resetImg.color = new Color(0.3f, 0.32f, 0.36f);
        var resetBtn = resetRt.gameObject.AddComponent<Button>();
        resetBtn.targetGraphic = resetImg;
        var resetLabel = NewText("Label", resetRt, "RESET", 28, TextAlignmentOptions.Center);
        Fill(resetLabel.rectTransform);
        resetLabel.raycastTarget = false;

        // Close button (top-right): lets the player back out when opened from the 3D game.
        var closeRt = NewUI("CloseButton", panel);
        Place(closeRt, new Vector2(1f, 1f), new Vector2(60, 60), new Vector2(-20, -20));
        closeRt.pivot = new Vector2(1f, 1f);
        var closeImg = closeRt.gameObject.AddComponent<Image>();
        closeImg.color = new Color(0.55f, 0.15f, 0.15f);
        var closeBtn = closeRt.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var closeLabel = NewText("Label", closeRt, "X", 32, TextAlignmentOptions.Center);
        Fill(closeLabel.rectTransform);
        closeLabel.raycastTarget = false;

        // Controller + serialized references
        var ctrl = canvasGo.AddComponent<AlarmPuzzleController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("resetButton").objectReferenceValue = resetBtn;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        var wiresProp = so.FindProperty("wires");
        wiresProp.arraySize = views.Length;
        for (int i = 0; i < views.Length; i++)
        {
            var e = wiresProp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("color").enumValueIndex = (int)views[i].color;
            e.FindPropertyRelative("button").objectReferenceValue = views[i].button;
            e.FindPropertyRelative("image").objectReferenceValue = views[i].image;
            e.FindPropertyRelative("label").objectReferenceValue = views[i].label;
            e.FindPropertyRelative("cutMarker").objectReferenceValue = views[i].cutMarker;
            e.FindPropertyRelative("activeColor").colorValue = views[i].activeColor;
        }
        so.ApplyModifiedProperties();

        // EventSystem: the scene already has one; add only if missing.
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
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

    // Anchor to a single point (anchor), fixed size, offset from the anchor.
    static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y == 1f ? 1f : anchor.y == 0f ? 0f : 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void Stretch(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static void Fill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
