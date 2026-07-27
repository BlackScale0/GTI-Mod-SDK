using UnityEngine;

public class ISTMapConfig : MonoBehaviour
{
    [Header("Loot Budget")]
    [Tooltip("Minimum number of loot points the game fills with items each round (clamped to the number of LootSpawn markers present).")]
    [Min(0)] public int minLootSpawns = 8;
    [Tooltip("Maximum number of loot points filled each round (clamped to the number of LootSpawn markers present).")]
    [Min(0)] public int maxLootSpawns = 16;

    [Header("NavMesh")]
    [Tooltip("If true, the game bakes a runtime NavMesh over this map after it loads. Leave on unless the map ships its own baked NavMesh data.")]
    public bool bakeRuntimeNavMesh = true;
}
