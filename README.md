<p align="center">
  <img src="logo.jpg" alt="Idiots Stealing Things" width="640">
</p>

<h1 align="center">Mod SDK</h1>

<p align="center">
  <b>A chaotic free-for-all heist game where you backstab your friends for the biggest payout.</b><br>
  Trigger fire alarms, tase your rivals, and steal their loot right out of their hands.<br>
  Race to the getaway van before the cops arrive, because until you extract, nothing you stole is truly yours.
</p>

<p align="center">
  <a href="https://store.steampowered.com/app/4919380/Idiots_Stealing_Things/">
    <img src="https://img.shields.io/badge/Steam-Wishlist%20Now-1b2838?style=for-the-badge&logo=steam&logoColor=white" alt="Steam">
  </a>
  <a href="https://discord.gg/azsScpGF5p">
    <img src="https://img.shields.io/badge/Discord-Join%20the%20Idiots-5865F2?style=for-the-badge&logo=discord&logoColor=white" alt="Discord">
  </a>
  <img src="https://img.shields.io/badge/Unity-6000.x-000000?style=for-the-badge&logo=unity&logoColor=white" alt="Unity 6">
  <img src="https://img.shields.io/badge/BepInEx-6.0.0--be.755-a05a2c?style=for-the-badge" alt="BepInEx">
  <img src="https://img.shields.io/badge/License-MIT-4c9a2a?style=for-the-badge" alt="MIT License">
</p>

---

There are two different things you can make for this game, and they work differently.

| | What it is | Ships as | Where to start |
|---|---|---|---|
| **Mods** | Code. BepInEx plugins with full access to the game. | `.dll` | Sections 1 to 7 |
| **Custom maps** | Data. No code, ever. | AssetBundle + JSON | Section 8 |

If you only want to build a map, skip straight to section 8.

---

## 1. Install the correct BepInEx version

Idiots Stealing Things is built on **Unity 6 (6000.x), Mono, 64-bit**, so it requires a compatible BepInEx build.

Use **BepInEx-Unity.Mono-win-x64-6.0.0-be.755+3fab71a**:

https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.Mono-win-x64-6.0.0-be.755%2B3fab71a.zip

### Setup steps

1. Download BepInEx
2. Extract all files into the game root folder (where the `.exe` is located)
3. Launch the game once to generate required folders (`BepInEx`, `plugins`, configs, etc.)

---

## 2. Installing mods (manual method)

Mods are compiled as `.dll` files created from a BepInEx plugin project.

1. Build the mod project in Visual Studio
2. Find the output `.dll`, usually in `bin/Release` or `bin/Debug`
3. Place the `.dll` into the plugins folder:

```
C:\Program Files (x86)\Steam\steamapps\common\Idiots Stealing Things\BepInEx\plugins
```

4. Start the game
5. Mods load automatically on startup if installed correctly

---

## 3. Installing mods (Thunderstore / r2modman)

Mods can also be installed using a mod manager, either **Thunderstore Mod Manager** or **r2modman**.

1. Install either manager
2. Create a profile for **Idiots Stealing Things**
3. Install mods directly through the manager
4. Click **Start Modded** to launch the game with mods enabled

This method handles installation paths and dependencies automatically, making it easier than manual setup.

---

<details>
<summary><b>4. Folder structure reference</b></summary>

<br>

After installing BepInEx correctly, your game directory should look like this:

```
Idiots Stealing Things/
├── BepInEx/
│   ├── config/
│   ├── plugins/   <- mods and maps go here
│   ├── LogOutput.log
├── doorstop_config.ini
├── winhttp.dll
├── Idiots Stealing Things.exe
```

</details>

---

## 5. Accessing game code: reflection vs DLL reference

There are two ways to interact with the game's code from a mod.

**Reflection** finds and accesses types and fields at runtime without any direct reference to the game's assemblies. It compiles with just BepInEx and UnityEngine, so your mod will not break if the game updates and the DLLs change. The tradeoff is more boilerplate and no compiler help if a field name changes. See `ReflectionExample` for a working demonstration.

**DLL reference** means adding `Assembly-CSharp.dll` (and any others you need) directly to your project. You get full IntelliSense, compile-time checks, and much simpler code. The tradeoff is that your mod will fail to compile if those DLLs change between game updates. See `DllExample` for a working demonstration.

For simple local mods, or anything that only touches a few fields, reflection is fine. For larger mods that interact with many game systems, a DLL reference is usually easier to work with.

### Setting up a DLL reference

The game DLLs are in:

```
Idiots Stealing Things/Idiots Stealing Things_Data/Managed/
```

The ones you will most commonly need:

| DLL | Contains |
|-----|----------|
| `Assembly-CSharp.dll` | All game-specific code (PlayerController, ISTNetworkManager, etc.) |
| `Mirror.dll` | Networking (NetworkBehaviour, NetworkServer, etc.) |
| `UnityEngine.CoreModule.dll` | Core Unity types |

Add them to your `.csproj` like this, with `Private=false` so they are not bundled into your output DLL:

```xml
<Reference Include="Assembly-CSharp">
  <HintPath>$(ManagedDir)\Assembly-CSharp.dll</HintPath>
  <Private>false</Private>
</Reference>
<Reference Include="Mirror">
  <HintPath>$(ManagedDir)\Mirror.dll</HintPath>
  <Private>false</Private>
</Reference>
```

`Private=false` is important. The game already has these DLLs at runtime, and bundling them into your mod would cause conflicts.

---

## 6. Host and client mod matching

Idiots Stealing Things checks mods during the join handshake. By default, **a mod must be installed on the host and every client at the same version**. If you join a host whose mods you do not have, or the other way around, you are rejected with a list of what to fix.

A mod can relax that by declaring a compatibility level. Add a single `const string` named `ISTCompat` to your plugin class:

```csharp
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string ISTCompat = "ClientOnly";
}
```

| Value | Meaning | Use for |
|-------|---------|---------|
| `Everyone` *(default if omitted)* | Host and every client must have it, versions must match | New content, gameplay changes, anything that syncs |
| `ClientOnly` | Purely local, never checked by the join gate | FOV, keybinds, speed mods, cosmetic-only UI |
| `ServerOnly` | Only the host needs it, clients do not | Host-side economy and spawn tweaks with no client code |

Notes:

* The value is case-insensitive, so `"client"`, `"server"` and `"all"` also work.
* Omitting `ISTCompat`, or typing it wrong, is treated as `Everyone`, which is the safe default.
* When in doubt, leave it as `Everyone`. Only mark a mod `ClientOnly` if it genuinely has no effect on anyone else's game.
* **Custom maps are not mods and never touch this gate.** Missing a map does not stop you joining a lobby. The game runs its own availability check once a host actually picks the map, covered in section 8.

---

## 7. Example mods

Two example projects are included, showing the same mod written both ways.

### ReflectionExample

A local player speed boost toggled with F1. It demonstrates how to access game types and fields at runtime without referencing any game DLLs directly. The mod compiles with only BepInEx and UnityEngine, making it resilient to game updates.

Declared as `ClientOnly` because it only affects the local player's movement and nothing is synced over the network.

### DllExample

The same speed boost, rewritten using a direct `Assembly-CSharp.dll` reference. The code is simpler and you get full IntelliSense and compile-time checks, but the mod will break if the game updates and those fields change. Compare this side by side with `ReflectionExample` to understand the tradeoff.

Also declared `ClientOnly`.

---

## 8. Custom maps

A map is **data**, not code. It ships as an AssetBundle plus a small JSON manifest sitting next to it, with no plugin and no DLL involved:

```
BepInEx/plugins/CoolMapPack/
├── demomap_map              <- your exported bundle
└── demomap_map.istmap.json  <- the manifest
```

```json
{
    "id": "com.yourname.demomap",
    "name": "Demo Map",
    "version": "1.0.0",
    "bundle": "demomap_map"
}
```

The SDK's exporter writes both files for you, so you never hand-edit the JSON. At startup the game scans `BepInEx/plugins` (including subfolders) for `*.istmap.json` and registers every map it finds, then handles the rest at runtime: listing it in the lobby's Match Settings map picker, warning players who do not have it, loading the bundle on every machine, baking the NavMesh, turning your dummy markers into real cameras, vans and loot, and injecting all the heist managers.

| Field | Required | Purpose |
|---|---|---|
| `id` | yes | Unique, reverse-domain, and **never changes** between versions. Games match maps by this. |
| `name` | no | Shown in the lobby picker. Defaults to the id. |
| `version` | no | Defaults to `1.0.0`. Players on different versions count as missing the map. |
| `bundle` | yes | Bundle filename, relative to the manifest. Must stay inside the manifest's own folder. |

A map never blocks the lobby. Players without it can still join and gather. When the host selects your map, anyone missing it sees an on-screen "MISSING MAP" notice below the timer, and the host cannot start the round until everyone has installed it.

### Why maps do not use plugins

A BepInEx plugin is arbitrary code with full access to the machine it runs on. That is fine and necessary for gameplay mods, but a map does not need any of it, and if maps shipped as plugins too, players would have no way to tell the two apart. So they do not:

> A map is a bundle and a JSON file. If something calling itself a map ships a DLL, that DLL is doing something other than being a map.

**Maps carry no scripts.** When the game loads a custom map it strips every behaviour component from the scene, along with UnityEvent hooks and animation events, keeping only geometry, colliders, lights, audio, particles, renderers and the SDK's own marker components. This is enforced at runtime, so it holds regardless of what ends up in your bundle. The exporter warns you at export time if your scene contains anything that will be removed. Build gameplay out of the SDK's markers and NpcZones, and movement out of Animator or Animation.

None of this makes installing mods risk-free. A gameplay mod is still code you are choosing to run, and you should get mods from sources you trust. It just means installing a *map* is not that.

### Building a custom map (IST_MapSDK)

Maps are built in the separate **IST_MapSDK** Unity project, not the full game project. It runs the same Unity version (6000.x) and is preconfigured so you can start immediately. It ships the dummy prefabs, the marker and zone scripts, and the exporter, plus an example scene at `Project/Scenes/demomap` that demonstrates dummy placement, NpcZones, and a layout you can copy. The SDK mirrors the game's folder layout, so `Project/Scenes/`, `Project/Scripts/` and the rest line up with the real project.

1. Open `IST_MapSDK` and open `Project/Scenes/demomap` for reference, or start a new scene.
2. Build your level. Import your own assets (3D models, materials, textures) and lay it out.
3. Place the dummy prefabs where things should appear in-game. Each carries an `ISTMapMarker`:

   | Dummy prefab | Becomes in-game |
   |---|---|
   | `Dummy_PlayerSpawn` | Where players spawn (place at least one, ideally at least 12) |
   | `Dummy_Van_Standard` | Extraction van (place one or two) |
   | `Dummy_LootSpawn` | A random sellable loot item |
   | `Dummy_Camera` | A security camera, aimed along the object's forward arrow |

4. Add `NpcZone` components for the crowd. **Public** zones go in walkable and hallway areas, where customers and police spawn, and **Store** zones go over shops. No zones means no NPCs.
5. Optionally, add one `ISTMapConfig` to a root object to tune the loot budget.
6. Save the scene, then run **IST > Export Current Map**. Fill in your map's **ID**, **display name** and **version**, which are remembered per scene so re-exporting is one click, then hit Export. It validates your markers, warns about any scripts the game will strip, excludes the default camera and light, and writes both files to `MapExports/`.

The bundle is named `<scene>` lower-cased plus `_map`, so a scene called `Demo` exports `demo_map`, and the manifest is that name plus `.istmap.json`.

### Installing the map

Deployment is manual on purpose, so re-exporting a map is just dropping in the new files. Put both in the plugins folder together. A subfolder is fine and keeps things tidy:

```
BepInEx/plugins/
└── DemoMap/
    ├── demomap_map
    └── demomap_map.istmap.json
```

The map then appears in the lobby's Match Settings map picker on every machine that has it.

To publish on Thunderstore, package that folder as normal. No DLL, and no dependency on BepInEx beyond the folder it lives in.

---

<details>
<summary><b>9. Troubleshooting</b></summary>

<br>

* Ensure mods match the game version.
* Check `BepInEx/LogOutput.log` for errors.
* Some mods require dependencies, which mod managers handle automatically.
* For maps, confirm the `bundle` value in the `.istmap.json` exactly matches the bundle file sitting next to it. A name mismatch logs "bundle ... not found" and is the most common reason a map will not load. Copy both files together, since neither works alone.
* If a map loads but is missing things you placed, check `LogOutput.log` for `[IST.Maps] Sanitizer: removed ...` lines. Custom maps may not carry script components, and the SDK exporter warns about this before you export.

</details>

---

<p align="center">
  <a href="https://store.steampowered.com/app/4919380/Idiots_Stealing_Things/"><b>Wishlist on Steam</b></a>
  &nbsp;&bull;&nbsp;
  <a href="https://discord.gg/azsScpGF5p"><b>Join the Discord</b></a>
</p>