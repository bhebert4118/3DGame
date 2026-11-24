using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private Transform promptAnchor;
    private Renderer[] renderers;

    public float InteractionDistance => interactionDistance;
    public Vector3 PromptPosition => promptAnchor != null ? promptAnchor.position : GetComponent<Collider>().bounds.center;
    public Bounds InteractionBounds => GetComponent<Collider>() != null
        ? GetComponent<Collider>().bounds
        : new Bounds(transform.position, Vector3.one * 0.5f);
    public Bounds VisualBounds
    {
        get
        {
            if (renderers == null || renderers.Length == 0) return InteractionBounds;
            bool found = false;
            Bounds result = default;
            foreach (Renderer target in renderers)
            {
                if (!target.enabled || !target.gameObject.activeInHierarchy) continue;
                if (!found) { result = target.bounds; found = true; }
                else result.Encapsulate(target.bounds);
            }
            return found ? result : InteractionBounds;
        }
    }

    public Vector3[] GetOutlineWorldPoints()
    {
        var points = new System.Collections.Generic.List<Vector3>();
        if (renderers != null)
        {
            foreach (Renderer target in renderers)
            {
                if (!target.enabled || !target.gameObject.activeInHierarchy) continue;
                MeshFilter filter = target.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null) continue;
                foreach (Vector3 vertex in mesh.vertices) points.Add(target.transform.TransformPoint(vertex));
            }
        }
        return points.Count > 0 ? points.ToArray() : BoundsCorners(InteractionBounds);
    }

    private static Vector3[] BoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min, max = bounds.max;
        return new[] {
            new Vector3(min.x,min.y,min.z), new Vector3(max.x,min.y,min.z),
            new Vector3(min.x,max.y,min.z), new Vector3(max.x,max.y,min.z),
            new Vector3(min.x,min.y,max.z), new Vector3(max.x,min.y,max.z),
            new Vector3(min.x,max.y,max.z), new Vector3(max.x,max.y,max.z)
        };
    }

    protected virtual void Awake()
    {
        // Interactables may be invisible collider volumes placed over a visible prop.
        // Use renderers on the object and its immediate visual parent as outline targets.
        renderers = transform.parent != null
            ? transform.parent.GetComponentsInChildren<Renderer>(false)
            : GetComponentsInChildren<Renderer>(false);
    }

    public void SetFocused(bool focused) { }

    public void Interact() => OnInteract();
    protected abstract void OnInteract();
}
