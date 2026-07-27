using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ISTMapMarker : MonoBehaviour
{
    public enum Kind
    {
        // NOTE: these values are serialized into authored custom maps. Only ever APPEND new
        // kinds; never reorder or reuse a number or existing maps will place the wrong object.
        PlayerSpawn = 0,
        SecurityCamera = 1,
        LootSpawn = 2,
        ExtractionVan = 3,
        SecurityComputer = 4, // Dummy_Computer   — CCTV monitor the detained player / free players watch.
        DetentionChair = 5,   // Dummy_DetainedChair — chair a cop sits an arrested player in.
        FireAlarm = 6,        // Dummy_FireAlarm  — pull switch that triggers the mall fire alarm.
        GarbageBin = 7,       // Dummy_GarbageBin — hideable dumpster (small or large, see binType).
    }

    /// <summary>Which dumpster prefab a <see cref="Kind.GarbageBin"/> marker becomes.</summary>
    public enum BinType
    {
        Small = 0, // Holds one player; a newcomer boots the current occupant out.
        Large = 1, // Holds unlimited players.
    }

    /// <summary>
    /// Which item sizes a <see cref="Kind.LootSpawn"/> point is allowed to hold. A bit flag so one
    /// point can accept several sizes (e.g. Carry + Haul but no Pocket). Loot whose item size isn't
    /// in this mask is never rolled at this point.
    /// </summary>
    [System.Flags]
    public enum LootSizeMask
    {
        Pocket = 1 << 0, // Small pocketable items.
        Carry = 1 << 1,  // Two-handed carry items.
        Haul = 1 << 2,   // Large two-player haul items.
    }

    public const LootSizeMask AllLootSizes = LootSizeMask.Pocket | LootSizeMask.Carry | LootSizeMask.Haul;

    [Tooltip("What real game object this dummy is replaced with at runtime.")]
    public Kind kind = Kind.PlayerSpawn;

    [Header("Loot Spawn")]
    [Tooltip("Relative chance this point is chosen when the game thins loot to its per-round budget. Higher = more likely to hold an item.")]
    [Min(0f)] public float lootWeight = 1f;
    [Tooltip("Which item sizes this point may spawn. Multi-select: a point can allow several sizes " +
             "(e.g. Carry + Haul but no Pocket). Loot whose size isn't ticked is never rolled here. " +
             "Default is all sizes.")]
    public LootSizeMask lootSizes = AllLootSizes;

    [Header("Security Camera")]
    [Tooltip("How far the camera can see, in metres. The runtime SecurityCamera uses this as its detection range.")]
    [Min(1f)] public float cameraDetectionRange = 18f;

    [Header("Garbage Bin")]
    [Tooltip("Small = one-player dumpster (a newcomer evicts the occupant). Large = unlimited capacity.")]
    public BinType binType = BinType.Small;

#if UNITY_EDITOR
    private static readonly Color[] KindColors =
    {
        new Color(1.00f, 0.40f, 0.75f, 1f), // PlayerSpawn (Pink)
        new Color(1.00f, 0.20f, 0.20f, 1f), // SecurityCamera (Red)
        new Color(0.30f, 0.95f, 0.35f, 1f), // LootSpawn (Green)
        new Color(0.20f, 0.55f, 1.00f, 1f), // ExtractionVan (Blue)
        new Color(0.20f, 0.90f, 0.95f, 1f), // SecurityComputer (Cyan)
        new Color(0.95f, 0.85f, 0.20f, 1f), // DetentionChair (Yellow)
        new Color(1.00f, 0.55f, 0.10f, 1f), // FireAlarm (Orange)
        new Color(0.60f, 0.45f, 0.30f, 1f), // GarbageBin (Brown)
    };

    private void OnDrawGizmos()
    {
        Color c = KindColors[(int)kind];
        Gizmos.color = c;
        Gizmos.DrawSphere(transform.position, 0.12f);
        DrawArrow(transform.position, transform.forward, 0.6f);

        if (kind == Kind.SecurityCamera)
        {
            Gizmos.color = new Color(c.r, c.g, c.b, 0.18f);
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, 50f, cameraDetectionRange, 0.1f, 16f / 9f);
            Gizmos.matrix = prev;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Handles.color = Color.white;
        string label = $"IST {kind}";
        if (kind == Kind.GarbageBin) label += $" ({binType})";
        else if (kind == Kind.LootSpawn) label += $" ({LootSizesLabel()})";
        Handles.Label(transform.position + Vector3.up * 0.3f, label, EditorStyles.boldLabel);
    }

    private string LootSizesLabel()
    {
        if ((lootSizes & AllLootSizes) == AllLootSizes || lootSizes == 0) return "Any";
        return lootSizes.ToString();
    }

    private static void DrawArrow(Vector3 origin, Vector3 dir, float len)
    {
        Vector3 tip = origin + dir * len;
        Gizmos.DrawLine(origin, tip);
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(dir, Vector3.forward);
        right.Normalize();
        float h = len * 0.25f;
        Gizmos.DrawLine(tip, tip - dir * h + right * h * 0.5f);
        Gizmos.DrawLine(tip, tip - dir * h - right * h * 0.5f);
    }
#endif
}
