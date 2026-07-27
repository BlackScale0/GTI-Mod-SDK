using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Exports the open scene as a Grand Theft Idiots custom map: one AssetBundle plus a
/// <c>.istmap.json</c> manifest beside it. Those two files ARE the map — drop them in
/// BepInEx/plugins and the game picks them up. No plugin DLL, no C#.
///
/// The window collects the map's identity (id / display name / version), which used to live
/// in the map mod's Plugin.cs, and remembers it per scene so re-exporting is one click.
/// </summary>
public class MapExporter : EditorWindow
{
    private const string ExportDirectory = "MapExports";
    private const string PrefsPrefix = "IST.MapExporter.";

    private string _mapId;
    private string _displayName;
    private string _version;

    private string _scenePath;
    private string _sceneName;
    private Vector2 _scroll;

    [MenuItem("IST/Export Current Map")]
    public static void Open()
    {
        var window = GetWindow<MapExporter>(true, "Export Map", true);
        window.minSize = new Vector2(430f, 340f);
        window.LoadForActiveScene();
        window.Show();
    }

    // ---- Identity persistence ------------------------------------------------

    // Keyed by scene path so a project with several maps keeps each one's identity.
    private string PrefsKey(string field) => PrefsPrefix + _scenePath + "." + field;

    private void LoadForActiveScene()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        _scenePath = scene.path;
        _sceneName = scene.name;

        if (string.IsNullOrEmpty(_scenePath)) return;

        _mapId = EditorPrefs.GetString(PrefsKey("id"), "com.yourname." + Slug(_sceneName));
        _displayName = EditorPrefs.GetString(PrefsKey("name"), _sceneName);
        _version = EditorPrefs.GetString(PrefsKey("version"), "1.0.0");
    }

    private void SaveIdentity()
    {
        if (string.IsNullOrEmpty(_scenePath)) return;
        EditorPrefs.SetString(PrefsKey("id"), _mapId);
        EditorPrefs.SetString(PrefsKey("name"), _displayName);
        EditorPrefs.SetString(PrefsKey("version"), _version);
    }

    // ---- Window --------------------------------------------------------------

    private void OnGUI()
    {
        // The user can switch scenes while the window is open.
        if (EditorSceneManager.GetActiveScene().path != _scenePath)
            LoadForActiveScene();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        if (string.IsNullOrEmpty(_scenePath))
        {
            EditorGUILayout.HelpBox("Save your scene before exporting.", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.LabelField("Scene", _sceneName, EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Map identity", EditorStyles.boldLabel);
        _mapId = EditorGUILayout.TextField(new GUIContent("Map ID",
            "Unique id for your map, reverse-domain style. Players' games match maps by this, " +
            "so it must never change between versions of the same map."), _mapId);
        _displayName = EditorGUILayout.TextField(new GUIContent("Display Name",
            "What players see in the lobby's map picker."), _displayName);
        _version = EditorGUILayout.TextField(new GUIContent("Version",
            "Bump this when you re-release. Players on a different version count as missing the map."), _version);

        EditorGUILayout.Space();
        string bundleName = BundleName();
        EditorGUILayout.LabelField("Will export", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  " + bundleName);
        EditorGUILayout.LabelField("  " + bundleName + ".istmap.json");

        EditorGUILayout.Space();
        DrawIdentityWarnings();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(!IdentityValid()))
        {
            if (GUILayout.Button("Export Map", GUILayout.Height(32f)))
                Export();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawIdentityWarnings()
    {
        if (string.IsNullOrWhiteSpace(_mapId))
            EditorGUILayout.HelpBox("Map ID is required.", MessageType.Error);
        else if (_mapId.Any(char.IsWhiteSpace))
            EditorGUILayout.HelpBox("Map ID cannot contain spaces.", MessageType.Error);
        else if (_mapId.StartsWith("com.yourname.", StringComparison.OrdinalIgnoreCase))
            EditorGUILayout.HelpBox("Change 'com.yourname' to something of your own, or your map " +
                                    "may collide with someone else's.", MessageType.Warning);

        if (string.IsNullOrWhiteSpace(_version))
            EditorGUILayout.HelpBox("Version is required.", MessageType.Error);
    }

    private bool IdentityValid() =>
        !string.IsNullOrWhiteSpace(_mapId) &&
        !_mapId.Any(char.IsWhiteSpace) &&
        !string.IsNullOrWhiteSpace(_version);

    private string BundleName() => Slug(_sceneName) + "_map";

    private static string Slug(string s) => (s ?? "").ToLower().Replace(" ", "_");

    // ---- Export --------------------------------------------------------------

    private void Export()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            EditorUtility.DisplayDialog("Error", "You must save your Scene before exporting!", "OK");
            return;
        }

        if (!ValidateMarkers(out string markerReport, out bool fatal))
        {
            if (fatal)
            {
                EditorUtility.DisplayDialog("Map not ready", markerReport, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Map warnings", markerReport + "\n\nExport anyway?", "Export", "Cancel"))
                return;
        }

        // Anything the game will strip on load is worth knowing about HERE, not when a player
        // reports that your map is broken. Mirrors the game's MapSanitizer allowlist.
        if (FindStrippableComponents(out string behaviourReport))
        {
            if (!EditorUtility.DisplayDialog("Scripts in map", behaviourReport, "Export anyway", "Cancel"))
                return;
        }

        SaveIdentity();
        AutoTagEditorOnlyObjects(activeScene);

        if (!Directory.Exists(ExportDirectory))
            Directory.CreateDirectory(ExportDirectory);

        string bundleName = BundleName();

        EditorUtility.DisplayProgressBar("IST Map Exporter", "Compiling Map AssetBundle...", 0.5f);
        try
        {
            // An explicit build map builds exactly this one bundle. The old approach assigned
            // importer.assetBundleName and called the build-everything overload, which also
            // rebuilt any other bundle name left assigned anywhere in the project (and leaked
            // that assignment if the build threw).
            var build = new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = new[] { activeScene.path },
            };

            AssetBundleManifest result = BuildPipeline.BuildAssetBundles(
                ExportDirectory,
                new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression,
                BuildTarget.StandaloneWindows64);

            if (result == null)
            {
                Debug.LogError("[IST SDK] Map Export Failed: the AssetBundle build returned no manifest. " +
                               "Check the Console for import errors in your scene.");
                EditorUtility.DisplayDialog("Build Failed", "Something went wrong. Check the Unity Console for details.", "OK");
                return;
            }

            string manifestPath = WriteManifest(bundleName);

            Debug.Log($"[IST SDK] Map exported to: {Path.GetFullPath(Path.Combine(ExportDirectory, bundleName))}");
            EditorUtility.RevealInFinder(Path.Combine(ExportDirectory, bundleName));

            EditorUtility.DisplayDialog("Success!",
                $"Map '{_displayName}' exported.\n\n" +
                $"Ship these two files together:\n\n  {bundleName}\n  {Path.GetFileName(manifestPath)}\n\n" +
                "Put both in BepInEx/plugins (a subfolder is fine). The game finds them at " +
                "startup and lists your map in the lobby picker. No DLL needed.\n\n" +
                "(Default cameras and lights were excluded automatically.)", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[IST SDK] Map Export Failed: {e.Message}");
            EditorUtility.DisplayDialog("Build Failed", "Something went wrong. Check the Unity Console for details.", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    [Serializable]
    private class MapManifest
    {
        public string id;
        public string name;
        public string version;
        public string bundle;
    }

    /// <summary>Writes the .istmap.json next to the bundle and returns its path.</summary>
    private string WriteManifest(string bundleName)
    {
        var manifest = new MapManifest
        {
            id = _mapId.Trim(),
            name = string.IsNullOrWhiteSpace(_displayName) ? _mapId.Trim() : _displayName.Trim(),
            version = _version.Trim(),
            bundle = bundleName,
        };

        string path = Path.Combine(ExportDirectory, bundleName + ".istmap.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        return path;
    }

    private static void AutoTagEditorOnlyObjects(Scene activeScene)
    {
        bool sceneModified = false;

        foreach (Camera cam in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if ((cam.CompareTag("MainCamera") || cam.name.Contains("Main Camera")) && !cam.CompareTag("EditorOnly"))
            {
                cam.tag = "EditorOnly";
                Debug.Log("[IST SDK] Auto-tagged Camera to 'EditorOnly' so it doesn't fight the game's camera.");
                sceneModified = true;
            }
        }

        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional && light.name.Contains("Directional Light") && !light.CompareTag("EditorOnly"))
            {
                light.tag = "EditorOnly";
                Debug.Log("[IST SDK] Auto-tagged Directional Light to 'EditorOnly' so it doesn't fight the Time of Day system.");
                sceneModified = true;
            }
        }

        if (sceneModified || activeScene.isDirty)
            EditorSceneManager.SaveScene(activeScene);
    }

    // ---- Validation ----------------------------------------------------------

    private static bool ValidateMarkers(out string report, out bool fatal)
    {
        ISTMapMarker[] markers = FindObjectsByType<ISTMapMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int spawns  = markers.Count(m => m.kind == ISTMapMarker.Kind.PlayerSpawn);
        int cameras = markers.Count(m => m.kind == ISTMapMarker.Kind.SecurityCamera);
        int loot    = markers.Count(m => m.kind == ISTMapMarker.Kind.LootSpawn);
        int vans    = markers.Count(m => m.kind == ISTMapMarker.Kind.ExtractionVan);

        var sb = new StringBuilder();
        fatal = false;

        if (spawns == 0)
        {
            sb.AppendLine("• No Dummy_PlayerSpawn markers players would spawn at the origin. Add at least one (ideally one per player).");
            fatal = true;
        }
        if (vans == 0)
            sb.AppendLine("• No Dummy_Van_Standard markers players will have no way to extract. Add at least one or two.");
        if (loot == 0)
            sb.AppendLine("• No Dummy_LootSpawn markers there will be nothing to steal. Add some.");
        if (cameras == 0)
            sb.AppendLine("• No Dummy_Camera markers that's allowed, the map just has no security cameras.");

        if (sb.Length == 0)
        {
            report = $"Markers OK: {spawns} spawns, {vans} vans, {loot} loot, {cameras} cameras.";
            return true;
        }

        report = $"Marker check ({spawns} spawns / {vans} vans / {loot} loot / {cameras} cameras):\n\n" + sb;
        return false;
    }

    /// <summary>
    /// The game treats a custom map as pure data and removes every behaviour component from it
    /// on load, so a map bundle can never carry logic. Authors need to hear about that at
    /// export time. Keep this list in sync with the game's MapSanitizer.
    /// </summary>
    private static readonly HashSet<string> AllowedGameTypes = new HashSet<string>(StringComparer.Ordinal)
    {
        "ISTMapMarker",
        "ISTMapConfig",
        "NpcZone",
    };

    private static readonly HashSet<string> AllowedAssemblies = new HashSet<string>(StringComparer.Ordinal)
    {
        "Unity.TextMeshPro",
        "Unity.AI.Navigation",
        "Unity.Mathematics",
        "Unity.Splines",
        "Unity.Burst",
    };

    private static readonly string[] DeniedAssemblyPrefixes =
    {
        "Unity.VisualScripting",
        "Unity.Timeline",
    };

    private static bool FindStrippableComponents(out string report)
    {
        var offenders = new SortedDictionary<string, int>(StringComparer.Ordinal);

        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue; // missing script — harmless, arrives inert in-game
                Type type = c.GetType();
                if (IsAllowed(type)) continue;

                offenders.TryGetValue(type.FullName, out int count);
                offenders[type.FullName] = count + 1;
            }
        }

        if (offenders.Count == 0)
        {
            report = null;
            return false;
        }

        var sb = new StringBuilder();
        sb.AppendLine("These components will be REMOVED by the game when your map loads, because a " +
                      "custom map is data and may not carry behaviour:");
        sb.AppendLine();
        foreach (KeyValuePair<string, int> kv in offenders)
            sb.AppendLine($"  • {kv.Key}  (x{kv.Value})");
        sb.AppendLine();
        sb.AppendLine("Anything that depends on them will not work in-game. Use the SDK's marker " +
                      "prefabs and NpcZones for gameplay, and Animator/Animation for movement.");
        report = sb.ToString();
        return true;
    }

    private static bool IsAllowed(Type type)
    {
        string assembly = type.Assembly.GetName().Name;

        foreach (string denied in DeniedAssemblyPrefixes)
            if (assembly.StartsWith(denied, StringComparison.Ordinal))
                return false;

        if (assembly == "UnityEngine" || assembly.StartsWith("UnityEngine.", StringComparison.Ordinal))
            return true;
        if (AllowedAssemblies.Contains(assembly))
            return true;

        return AllowedGameTypes.Contains(type.Name);
    }
}
