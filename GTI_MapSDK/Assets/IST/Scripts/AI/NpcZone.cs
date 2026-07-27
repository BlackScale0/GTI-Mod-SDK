using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Place on an empty GameObject inside any store, hallway, or exterior area.
/// Defines a flat region on the XZ plane that NPCs use as a wander area, spawn area, or
/// destination. Y height is used only for gizmo rendering.
///
/// SHAPE: By default a zone is a simple box (<see cref="size"/>, centred on the transform).
/// For rooms that are not a plain rectangle - T-shapes, L-shapes, two stores joined together,
/// anything with an odd footprint - switch it to an OUTLINE: a ring of corner points you drag
/// in the Scene view to trace the walls, just like editing a Polygon Collider. Select the zone
/// and:
///   - drag the round dots at each corner to move them,
///   - click a small "+" on an edge to add a corner there,
///   - click the small "x" on a corner to delete it.
/// Or press "Trace This Room (start outline)" in the Inspector to seed a square from the current
/// box that you can then reshape. Leave the outline empty and the zone stays a plain box, so all
/// existing zones are unchanged.
///
/// ZoneType controls how this zone is used:
///   Store    - Employee home zones and Customer browsing destinations.
///   Exterior - Where Customers walk when their shopping timer expires before despawning.
///   Public   - Common areas such as hallways. Customers and police spawn at a random
///              point inside these. Place several to spread spawns across the map.
///
/// isRestrictedArea marks zones as employee/staff-only. When a player is inside a
/// restricted zone and a police officer or employee sees them, the witness reacts
/// instantly (police aggro, employees phone in a report). Any zone type can be restricted.
///
/// storeExits (Store zones only) are hand-placed sub-boxes marking the doorways out of the
/// store. A player who steps into an exit box while carrying or hauling an item is treated
/// as walking out with stolen goods.
///
/// Scene setup:
///   Add one NpcZone per distinct area.
///   Employees reference their store NpcZone directly in the Inspector via homeZone.
///   Customers automatically find all Store and Exterior zones at runtime using GetAll().
///   The NpcSpawner finds all Public zones at runtime to place customers and police.
/// </summary>
public class NpcZone : MonoBehaviour
{
    public enum ZoneType { Store, Exterior, Public }

    /// <summary>
    /// A hand-placed doorway box on a Store zone, in the zone's local space. A player carrying
    /// loot who steps into one of these is leaving the store with stolen goods.
    /// </summary>
    [System.Serializable]
    public class ExitBox
    {
        [Tooltip("Centre of the exit box, relative to the zone's transform.")]
        public Vector3 localCenter = Vector3.zero;
        [Tooltip("Size of the exit box on the XZ plane (Y is used only for the gizmo).")]
        public Vector3 size = new Vector3(3f, 3f, 3f);
    }

    [SerializeField] private ZoneType zoneType = ZoneType.Store;
    [SerializeField] private Vector3 size = new Vector3(8f, 3f, 8f);
    [Tooltip("Optional custom outline. Leave empty for a plain box. When it has 3+ corners the " +
             "zone becomes this traced shape instead of the box - use it for T-shapes, L-shapes, " +
             "or joined rooms. Corners are XZ offsets relative to the transform; edit them in the " +
             "Scene view by dragging the dots and using the + / x buttons.")]
    [SerializeField] private List<Vector2> outline = new List<Vector2>();
    [Tooltip("Draw the traced outline as a filled translucent shape in the Scene view so it is " +
             "easier to see. Purely a visual aid; does not affect gameplay.")]
    [SerializeField] private bool fillOutline = true;
    [Tooltip("Marks this zone as restricted (e.g. staff-only back room). Employees and police react instantly when they see a player standing inside.")]
    [SerializeField] private bool isRestrictedArea = false;
    [Tooltip("Store zones only. Hand-placed doorway boxes marking the ways out of the store. A player carrying or hauling an item who steps into one is treated as walking out with stolen goods.")]
    [SerializeField] private ExitBox[] storeExits;

    public ZoneType Type => zoneType;
    public Vector3 Center => transform.position;
    public bool IsRestricted => isRestrictedArea;

    /// <summary>True when a custom outline (3+ corners) is defining the shape instead of the box.</summary>
    private bool HasOutline => outline != null && outline.Count >= 3;

    /// <summary>
    /// Full XZ size of the zone's bounding box. For a plain box this is <see cref="size"/>;
    /// for a traced outline it is the box that encloses every corner. Y is gizmo-only. Read by
    /// the room population system so generated layouts cover the whole footprint.
    /// </summary>
    public Vector3 Size
    {
        get
        {
            if (!HasOutline) return size;
            Vector2 min = outline[0], max = outline[0];
            for (int i = 1; i < outline.Count; i++)
            {
                min = Vector2.Min(min, outline[i]);
                max = Vector2.Max(max, outline[i]);
            }
            return new Vector3(max.x - min.x, size.y, max.y - min.y);
        }
    }

    /// <summary>Hand-placed doorway boxes (Store zones). Read by the room population system so
    /// generated layouts keep every exit clear.</summary>
    public ExitBox[] StoreExits => storeExits;

    /// <summary>
    /// The traced outline corners (XZ offsets relative to the transform), or null when the zone
    /// is a plain box. Read by the room population system, which treats each outline edge as a
    /// wall so generated furniture follows the real room shape.
    /// </summary>
    public IReadOnlyList<Vector2> OutlineCorners => HasOutline ? outline : null;

    private static readonly List<NpcZone> _registry = new List<NpcZone>();

    private void OnEnable() => _registry.Add(this);
    private void OnDisable() => _registry.Remove(this);

    public static NpcZone[] GetAll(ZoneType type)
    {
        List<NpcZone> result = new List<NpcZone>();
        foreach (NpcZone z in _registry)
            if (z.Type == type)
                result.Add(z);
        return result.ToArray();
    }

    /// <summary>
    /// Returns true if the given world position is inside any zone that has
    /// isRestrictedArea enabled. Called by NpcController during suspicion scanning.
    /// </summary>
    public static bool IsAnyRestrictedZone(Vector3 position)
    {
        foreach (NpcZone zone in _registry)
            if (zone.isRestrictedArea && zone.Contains(position))
                return true;
        return false;
    }

    /// <summary>
    /// Returns every zone marked isRestrictedArea (any ZoneType). Security officers use this to
    /// pick a staff-only area to do the rounds of; ordinary customers filter these out of their
    /// shopping and exit destinations so they never wander into a restricted area on their own.
    /// </summary>
    public static NpcZone[] GetAllRestricted()
    {
        List<NpcZone> result = new List<NpcZone>();
        foreach (NpcZone zone in _registry)
            if (zone.isRestrictedArea)
                result.Add(zone);
        return result.ToArray();
    }

    /// <summary>
    /// True if the world position sits inside one of this Store zone's hand-placed exit boxes.
    /// </summary>
    public bool IsAtStoreExit(Vector3 worldPoint)
    {
        if (storeExits == null) return false;

        foreach (ExitBox exit in storeExits)
        {
            if (exit == null) continue;
            Vector3 local = worldPoint - (transform.position + exit.localCenter);
            if (Mathf.Abs(local.x) <= exit.size.x * 0.5f && Mathf.Abs(local.z) <= exit.size.z * 0.5f)
                return true;
        }
        return false;
    }

    /// <summary>
    /// True if the world position is inside the main region of any Store zone. Used to tell
    /// whether a player carrying loot is still shopping (inside a store) or out in the open.
    /// </summary>
    public static bool IsInsideAnyStore(Vector3 position)
    {
        foreach (NpcZone zone in _registry)
            if (zone.zoneType == ZoneType.Store && zone.Contains(position))
                return true;
        return false;
    }

    /// <summary>
    /// Returns the Store zone whose region contains the position, or null if the position is
    /// not inside any store. Used by police to detect when they have wandered into a shop so they
    /// can do a short walkthrough before resuming patrol.
    /// </summary>
    public static NpcZone StoreZoneAt(Vector3 position)
    {
        foreach (NpcZone zone in _registry)
            if (zone.zoneType == ZoneType.Store && zone.Contains(position))
                return zone;
        return null;
    }

    /// <summary>
    /// Returns the Store zone whose exit box contains the position, or null if the position
    /// is not in any store exit. Used to detect a player walking loot out of a store.
    /// </summary>
    public static NpcZone StoreExitAt(Vector3 position)
    {
        foreach (NpcZone zone in _registry)
            if (zone.zoneType == ZoneType.Store && zone.IsAtStoreExit(position))
                return zone;
        return null;
    }

    /// <summary>A random point on the floor somewhere inside the zone (box or traced outline).</summary>
    public Vector3 RandomPoint()
    {
        if (!HasOutline)
        {
            float x = Random.Range(-size.x * 0.5f, size.x * 0.5f);
            float z = Random.Range(-size.z * 0.5f, size.z * 0.5f);
            return transform.position + new Vector3(x, 0f, z);
        }

        // Outline: reject-sample inside the bounding box until a point lands inside the shape.
        Vector3 s = Size;
        Vector2 boundsCenter = OutlineCenter();
        for (int i = 0; i < 32; i++)
        {
            Vector2 p = boundsCenter + new Vector2(
                Random.Range(-s.x * 0.5f, s.x * 0.5f),
                Random.Range(-s.z * 0.5f, s.z * 0.5f));
            if (PointInPolygon(p))
                return transform.position + new Vector3(p.x, 0f, p.y);
        }
        // Fallback: the average of the corners (always a reasonable interior-ish point).
        Vector2 avg = OutlineCenter();
        return transform.position + new Vector3(avg.x, 0f, avg.y);
    }

    /// <summary>True if the world point is inside the zone (box or traced outline), on the XZ plane.</summary>
    public bool Contains(Vector3 worldPoint)
    {
        Vector3 local = worldPoint - transform.position;
        if (!HasOutline)
            return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.z * 0.5f;
        return PointInPolygon(new Vector2(local.x, local.z));
    }

    // --- Outline helpers ---------------------------------------------------------------------

    /// <summary>Standard ray-casting point-in-polygon test on the local XZ outline.</summary>
    private bool PointInPolygon(Vector2 p)
    {
        bool inside = false;
        int n = outline.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            Vector2 a = outline[i], b = outline[j];
            if (((a.y > p.y) != (b.y > p.y)) &&
                (p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x))
                inside = !inside;
        }
        return inside;
    }

    private Vector2 OutlineCenter()
    {
        Vector2 sum = Vector2.zero;
        foreach (Vector2 c in outline) sum += c;
        return sum / Mathf.Max(1, outline.Count);
    }

    /// <summary>
    /// Ear-clipping triangulation of a simple (non-self-intersecting) polygon into a flat index
    /// list (triples). Used only to draw the translucent fill; supports concave T/L shapes.
    /// </summary>
    private static List<int> Triangulate(List<Vector2> pts)
    {
        List<int> indices = new List<int>();
        int n = pts.Count;
        if (n < 3) return indices;

        int[] V = new int[n];
        // Work with a consistent (counter-clockwise) winding.
        float area = 0f;
        for (int p = n - 1, q = 0; q < n; p = q++)
            area += pts[p].x * pts[q].y - pts[q].x * pts[p].y;
        if (area > 0f) for (int v = 0; v < n; v++) V[v] = v;
        else for (int v = 0; v < n; v++) V[v] = (n - 1) - v;

        int nv = n;
        int guard = 2 * nv;
        for (int v = nv - 1; nv > 2;)
        {
            if (guard-- <= 0) break; // Not a simple polygon; bail out gracefully.

            int u = v; if (nv <= u) u = 0;
            v = u + 1; if (nv <= v) v = 0;
            int w = v + 1; if (nv <= w) w = 0;

            if (Snip(pts, u, v, w, nv, V))
            {
                indices.Add(V[u]); indices.Add(V[v]); indices.Add(V[w]);
                for (int s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t];
                nv--;
                guard = 2 * nv;
            }
        }
        return indices;
    }

    private static bool Snip(List<Vector2> pts, int u, int v, int w, int n, int[] V)
    {
        Vector2 A = pts[V[u]], B = pts[V[v]], C = pts[V[w]];
        if (Mathf.Epsilon > (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x)) return false;
        for (int p = 0; p < n; p++)
        {
            if (p == u || p == v || p == w) continue;
            if (PointInTriangle(A, B, C, pts[V[p]])) return false;
        }
        return true;
    }

    private static bool PointInTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax = C.x - B.x, ay = C.y - B.y;
        float bx = A.x - C.x, by = A.y - C.y;
        float cx = B.x - A.x, cy = B.y - A.y;
        float apx = P.x - A.x, apy = P.y - A.y;
        float bpx = P.x - B.x, bpy = P.y - B.y;
        float cpx = P.x - C.x, cpy = P.y - C.y;
        float aCrossBp = ax * bpy - ay * bpx;
        float cCrossAp = cx * apy - cy * apx;
        float bCrossCp = bx * cpy - by * cpx;
        return aCrossBp >= 0f && bCrossCp >= 0f && cCrossAp >= 0f;
    }

    private void OnDrawGizmos()
    {
        Color fill;
        switch (zoneType)
        {
            case ZoneType.Store:
                fill = new Color(0.2f, 0.85f, 0.3f, 0.12f);
                break;
            case ZoneType.Exterior:
                fill = new Color(0.2f, 0.45f, 0.95f, 0.12f);
                break;
            default:
                fill = new Color(0.95f, 0.65f, 0.15f, 0.12f);
                break;
        }

        if (isRestrictedArea)
            fill = Color.Lerp(fill, new Color(0.9f, 0.1f, 0.1f, 0.3f), 0.6f);

        Color wire = fill; wire.a = 0.75f;
        Vector3 origin = transform.position;

        if (!HasOutline)
        {
            Gizmos.color = fill;
            Gizmos.DrawCube(origin, size);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(origin, size);
        }
        else
        {
            // Draw the outline as a wireframe prism: floor ring, top ring, and vertical edges.
            float half = size.y * 0.5f;
            Gizmos.color = wire;
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                Vector3 aFloor = origin + new Vector3(a.x, -half, a.y);
                Vector3 bFloor = origin + new Vector3(b.x, -half, b.y);
                Vector3 aTop = origin + new Vector3(a.x, half, a.y);
                Vector3 bTop = origin + new Vector3(b.x, half, b.y);
                Gizmos.DrawLine(aFloor, bFloor);
                Gizmos.DrawLine(aTop, bTop);
                Gizmos.DrawLine(aFloor, aTop);
            }

#if UNITY_EDITOR
            // Optional translucent fill so the shape reads as a solid area (handles concave shapes).
            if (fillOutline)
            {
                Color solid = fill; solid.a = 0.15f;
                UnityEditor.Handles.color = solid;
                List<int> tris = Triangulate(outline);
                for (int t = 0; t + 2 < tris.Count; t += 3)
                {
                    Vector2 a = outline[tris[t]], b = outline[tris[t + 1]], c = outline[tris[t + 2]];
                    UnityEditor.Handles.DrawAAConvexPolygon(
                        origin + new Vector3(a.x, 0f, a.y),
                        origin + new Vector3(b.x, 0f, b.y),
                        origin + new Vector3(c.x, 0f, c.y));
                }
            }
#endif
        }

        // Draw store exit boxes (bright green) so doorways are easy to place and read.
        if (zoneType == ZoneType.Store && storeExits != null)
        {
            foreach (ExitBox exit in storeExits)
            {
                if (exit == null) continue;
                Vector3 center = origin + exit.localCenter;
                Gizmos.color = new Color(0.1f, 1f, 0.3f, 0.18f);
                Gizmos.DrawCube(center, exit.size);
                Gizmos.color = new Color(0.1f, 1f, 0.3f, 0.8f);
                Gizmos.DrawWireCube(center, exit.size);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        string tag = isRestrictedArea ? " [RESTRICTED]" : "";
        Handles.Label(
            transform.position + Vector3.up * (Size.y * 0.5f + 0.25f),
            $"{zoneType}: {gameObject.name}{tag}",
            EditorStyles.boldLabel);
    }
#endif
}

#if UNITY_EDITOR
/// <summary>
/// Blender-style Scene-view editing for a zone's footprint. The box always shows draggable corner
/// dots (even before you have added anything). Click a "+" on any edge to add a corner there,
/// shift-click dots to select several, drag the shared arrows to move the whole selection, and
/// press Delete to remove selected corners. The first edit turns the plain box into an editable
/// outline automatically - there is no mode to toggle.
/// </summary>
[CustomEditor(typeof(NpcZone))]
public class NpcZoneEditor : Editor
{
    // Which corners are currently selected. Editor-only, not serialised.
    private readonly HashSet<int> _selected = new HashSet<int>();

    private static readonly Color EdgeColor = new Color(0.25f, 0.85f, 0.35f, 1f);
    private static readonly Color DotColor = new Color(0.2f, 0.8f, 0.35f, 1f);
    private static readonly Color DotSelected = new Color(1f, 0.75f, 0.15f, 1f);
    private static readonly Color AddColor = new Color(0.35f, 0.9f, 0.45f, 1f);

    private void OnSceneGUI()
    {
        var zone = (NpcZone)target;
        SerializedObject so = new SerializedObject(zone);
        SerializedProperty outline = so.FindProperty("outline");
        SerializedProperty size = so.FindProperty("size");
        if (outline == null || size == null) return;
        so.Update();

        Vector3 origin = zone.transform.position;
        List<Vector2> pts = WorkingCorners(outline, size);
        int n = pts.Count;
        if (n < 3) return;

        // Drop any stale selection indices (after a delete/insert).
        _selected.RemoveWhere(i => i >= n);

        Event e = Event.current;

        // --- Edges + the "+" add buttons at each edge midpoint. ---
        Handles.color = EdgeColor;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i], b = pts[(i + 1) % n];
            Vector3 aW = origin + new Vector3(a.x, 0f, a.y);
            Vector3 bW = origin + new Vector3(b.x, 0f, b.y);
            Handles.DrawAAPolyLine(4f, aW, bW);

            Vector2 mid = (a + b) * 0.5f;
            Vector3 midW = origin + new Vector3(mid.x, 0f, mid.y);
            float ms = HandleUtility.GetHandleSize(midW) * 0.08f;
            Handles.color = AddColor;
            if (Handles.Button(midW, Quaternion.identity, ms, ms * 1.4f, AddCap))
            {
                EnsureOutline(outline, size);
                outline.InsertArrayElementAtIndex(i + 1);
                outline.GetArrayElementAtIndex(i + 1).vector2Value = mid;
                so.ApplyModifiedProperties();
                _selected.Clear();
                _selected.Add(i + 1);
                return;
            }
            Handles.color = EdgeColor;
        }

        // --- Corner dots (click to select, shift-click to add/remove). ---
        for (int i = 0; i < n; i++)
        {
            Vector3 world = origin + new Vector3(pts[i].x, 0f, pts[i].y);
            float hs = HandleUtility.GetHandleSize(world) * 0.11f;
            Handles.color = _selected.Contains(i) ? DotSelected : DotColor;
            if (Handles.Button(world, Quaternion.identity, hs, hs, Handles.SphereHandleCap))
            {
                if (e.shift)
                {
                    if (!_selected.Add(i)) _selected.Remove(i);
                }
                else
                {
                    _selected.Clear();
                    _selected.Add(i);
                }
            }
        }

        // --- Shared move handle: drags every selected corner together. ---
        if (_selected.Count > 0)
        {
            Vector2 c = Vector2.zero;
            foreach (int i in _selected) c += pts[i];
            c /= _selected.Count;
            Vector3 handleW = origin + new Vector3(c.x, 0f, c.y);

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(handleW, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector2 delta = new Vector2(moved.x - handleW.x, moved.z - handleW.z);
                EnsureOutline(outline, size);
                foreach (int i in _selected)
                {
                    SerializedProperty p = outline.GetArrayElementAtIndex(i);
                    p.vector2Value = p.vector2Value + delta;
                }
                so.ApplyModifiedProperties();
            }
        }

        // --- Delete key removes the selected corners (keeps at least 3). ---
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            && _selected.Count > 0)
        {
            EnsureOutline(outline, size);
            List<int> ordered = new List<int>(_selected);
            ordered.Sort();
            for (int k = ordered.Count - 1; k >= 0; k--)
                if (outline.arraySize > 3)
                    outline.DeleteArrayElementAtIndex(ordered[k]);
            _selected.Clear();
            so.ApplyModifiedProperties();
            e.Use();
        }
    }

    /// <summary>Screen-facing cross so "+" buttons read clearly as "add".</summary>
    private static void AddCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
    {
        Handles.DotHandleCap(controlID, position, rotation, size, eventType);
    }

    /// <summary>The corners to draw: the real outline if present, otherwise the four box corners.</summary>
    private static List<Vector2> WorkingCorners(SerializedProperty outline, SerializedProperty size)
    {
        var list = new List<Vector2>();
        if (outline.arraySize >= 3)
        {
            for (int i = 0; i < outline.arraySize; i++)
                list.Add(outline.GetArrayElementAtIndex(i).vector2Value);
        }
        else
        {
            Vector3 s = size.vector3Value;
            float hx = s.x * 0.5f, hz = s.z * 0.5f;
            list.Add(new Vector2(-hx, -hz));
            list.Add(new Vector2(hx, -hz));
            list.Add(new Vector2(hx, hz));
            list.Add(new Vector2(-hx, hz));
        }
        return list;
    }

    /// <summary>Materialise the four box corners into the outline the first time it is edited.</summary>
    private static void EnsureOutline(SerializedProperty outline, SerializedProperty size)
    {
        if (outline.arraySize >= 3) return;
        Vector3 s = size.vector3Value;
        float hx = s.x * 0.5f, hz = s.z * 0.5f;
        outline.ClearArray();
        Insert(outline, 0, new Vector2(-hx, -hz));
        Insert(outline, 1, new Vector2(hx, -hz));
        Insert(outline, 2, new Vector2(hx, hz));
        Insert(outline, 3, new Vector2(-hx, hz));
    }

    private static void Insert(SerializedProperty list, int index, Vector2 value)
    {
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).vector2Value = value;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var zone = (NpcZone)target;
        SerializedObject so = new SerializedObject(zone);
        SerializedProperty outline = so.FindProperty("outline");

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Shape editing (Scene view):\n" +
            "- Drag a green dot to move that corner.\n" +
            "- Click a + on an edge to add a corner there.\n" +
            "- Shift-click dots to select several, then drag the arrows to move them together.\n" +
            "- Select corners and press Delete to remove them.\n" +
            "A plain box needs nothing set up - just start dragging. Use this for T-shapes, " +
            "L-shapes, or two rooms joined together.",
            MessageType.Info);

        if (outline.arraySize >= 3 && GUILayout.Button("Reset Shape To Box"))
        {
            outline.ClearArray();
            so.ApplyModifiedProperties();
            _selected.Clear();
        }
    }
}
#endif
