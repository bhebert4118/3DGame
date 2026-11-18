using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private float rayDistance = 4f;
    private Camera viewCamera;
    private Interactable focused;
    private Texture2D keycapTexture;
    private float focusedHitDistance = float.PositiveInfinity;

    private void Start() => ApplyCursorState();

    private void Awake()
    {
        viewCamera = GetComponent<Camera>();
        keycapTexture = CreateRoundedKeycap(32, 3, new Color(0f, 0f, 0f, 0.68f));
    }

    private void Update()
    {
        ApplyCursorState();
        if (AlarmPuzzleLauncher.IsOpen)
        {
            SetFocused(null);
            return;
        }
        Interactable next = null;
        float nextHitDistance = float.PositiveInfinity;
        Ray ray = viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
        {
            next = hit.collider.GetComponentInParent<Interactable>();
            if (next != null) nextHitDistance = hit.distance;
        }
        if (next != focused) SetFocused(next);
        focusedHitDistance = nextHitDistance;

        bool inReach = focused != null && focusedHitDistance <= focused.InteractionDistance;
        if (inReach && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) focused.Interact();
    }

    private void SetFocused(Interactable next)
    {
        if (focused == next) return;
        if (focused != null) focused.SetFocused(false);
        focused = next;
        if (focused != null) focused.SetFocused(true);
    }

    private static void ApplyCursorState()
    {
        bool puzzleOpen = AlarmPuzzleLauncher.IsOpen;
        Cursor.lockState = puzzleOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = puzzleOpen;
    }

    private void OnGUI()
    {
        if (AlarmPuzzleLauncher.IsOpen) return;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(Screen.width * .5f - 3f, Screen.height * .5f - 3f, 6f, 6f), Texture2D.whiteTexture);
        if (focused == null) return;
        Bounds visualBounds = focused.VisualBounds;
        Rect bounds = GetScreenBounds(visualBounds);
        bool inReach = focusedHitDistance <= focused.InteractionDistance;
        if (inReach && bounds.width > 0f && bounds.height > 0f)
            DrawProjectedContour(focused.GetOutlineWorldPoints());
        if (inReach)
        {
            const float keySize = 20f;
            Vector3 fallbackScreen = viewCamera.WorldToScreenPoint(focused.PromptPosition);
            float keyX = bounds.width > 0f ? bounds.xMax + 8f : fallbackScreen.x + 18f;
            float keyY = bounds.height > 0f ? bounds.center.y : Screen.height - fallbackScreen.y;
            Rect key = new Rect(keyX, keyY - keySize * 0.5f, keySize, keySize);
            GUI.color = Color.white;
            GUI.DrawTexture(key, keycapTexture);
            DrawCenteredE(key);
        }
    }

    private static void DrawCenteredE(Rect key)
    {
        // Draw the glyph as centered strokes so GUI skin font padding cannot offset it.
        float glyphWidth = key.width * 0.42f;
        float glyphHeight = key.height * 0.58f;
        float stroke = Mathf.Max(1.5f, key.width * 0.09f);
        float left = key.center.x - glyphWidth * 0.5f;
        float top = key.center.y - glyphHeight * 0.5f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(left, top, stroke, glyphHeight), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left, top, glyphWidth, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left, key.center.y - stroke * 0.5f, glyphWidth * 0.82f, stroke), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(left, top + glyphHeight - stroke, glyphWidth, stroke), Texture2D.whiteTexture);
    }

    private void DrawProjectedContour(Vector3[] corners)
    {
        var points = new List<Vector2>(corners.Length);
        foreach (Vector3 corner in corners)
        {
            Vector3 p = viewCamera.WorldToScreenPoint(corner);
            if (p.z > 0f) points.Add(new Vector2(p.x, Screen.height - p.y));
        }
        if (points.Count < 3) return;
        points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        var hull = new List<Vector2>();
        foreach (Vector2 p in points)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        int lowerCount = hull.Count;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            Vector2 p = points[i];
            while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
        GUI.color = Color.white;
        for (int i = 0; i < hull.Count; i++) DrawLine(hull[i], hull[(i + 1) % hull.Count], 2f);
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
        => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    private static void DrawLine(Vector2 from, Vector2 to, float width)
    {
        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, from);
        GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = oldMatrix;
    }

    private static Texture2D CreateRoundedKeycap(int size, int radius, Color color)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float cx = Mathf.Clamp(x, radius, size - radius - 1);
            float cy = Mathf.Clamp(y, radius, size - radius - 1);
            bool inside = (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius;
            texture.SetPixel(x, y, inside ? color : Color.clear);
        }
        texture.Apply();
        return texture;
    }

    private Rect GetScreenBounds(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners = {
            new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
            new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
            new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
            new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
        };
        float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity;
        float yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;
        foreach (Vector3 corner in corners)
        {
            Vector3 p = viewCamera.WorldToScreenPoint(corner);
            if (p.z <= 0f) continue;
            float y = Screen.height - p.y;
            xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
            yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
        }
        if (float.IsInfinity(xMin)) return Rect.zero;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void OnDisable()
    {
        SetFocused(null);
    }

    private void OnDestroy()
    {
        if (keycapTexture != null) Destroy(keycapTexture);
    }
}
