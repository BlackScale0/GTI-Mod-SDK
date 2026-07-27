# Idiots Stealing Things - Mod SDK

[![Steam](https://img.shields.io/badge/Steam-Wishlist%20Now-1b2838?logo=steam)](https://store.steampowered.com/app/4919380/Idiots_Stealing_Things/)
[![Discord](https://img.shields.io/badge/Discord-Idiots%20Stealing%20Things-5865F2?logo=discord&logoColor=white)](https://discord.gg/azsScpGF5p)

![Idiots Stealing Things](logo.jpg)

A chaotic free-for-all heist game where you backstab your friends for the biggest payout.
Trigger fire alarms, tase your rivals, and steal their loot right out of their hands.
Race to the getaway van before the cops arrive, because until you extract, nothing you stole is truly yours.

---

There are two different things you can make for this game, and they work differently:

* **Mods** are code BepInEx plugins, shipped as `.dll`. Sections 1–7.
* **Custom maps** are data an AssetBundle plus a small JSON file, no code at all. Section 8.

If you only want to build a map, you can skip straight to section 8.

---

## 1. Install the correct BepInEx version

Idiots Stealing Things is built on **Unity 6 (6000.x), Mono, 64-bit**, so it requires a compatible BepInEx build:

Use:
**BepInEx-Unity.Mono-win-x64-6.0.0-be.755+3fab71a**

https://builds.bepinex.dev/projects/bepinex_be/755/BepInEx-Unity.Mono-win-x64-6.0.0-be.755%2B3fab71a.zip

### Setup steps:

1. Download BepInEx
2. Extract all files into the game root folder (where the `.exe` is located)
3. Launch the game once to generate required folders (`BepInEx`, `plugins`, configs, etc.)

---

## 2. Installing Mods (Manual Method)

Mods are compiled as `.dll` files created from a BepInEx plugin project.

### Steps:

1. Build the mod project (Visual Studio)
2. Find the output `.dll` (usually in `bin/Release` or `bin/Debug`)
3. Place the `.dll` into:

### Example Steam path:

```
C:\Program Files (x86)\Steam\steamapps\common\Idiots Stealing Things\BepInEx\plugins
```

4. Start the game
5. Mods load automatically on startup if installed correctly

---

## 3. Installing Mods (Thunderstore / R2 Method)

Mods can also be installed using mod managers:

* **Thunderstore Mod Manager**
* **r2modman**

### Steps:

1. Install either Thunderstore Mod Manager or r2modman
2. Create a profile for **Idiots Stealing Things**
3. Install mods directly through the manager
4. Click **Start Modded** to launch the game with mods enabled

This method handles installation paths and dependencies automatically, making it easier than manual setup.

---

## 4. Folder Structure Reference

After installing BepInEx correctly, your game directory should look like:

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

---

## 5. Accessing Game Code: Reflection vs DLL Reference

There are two ways to interact with the game's code from a mod.

**Reflection** finds and accesses types and fields at runtime without any direct reference to the game's assemblies. It compiles with just BepInEx and UnityEngine, so your mod won't break if the game updates and the DLLs change. The tradeoff is more boilerplate and no compiler help if a field name changes. See `ReflectionExample` for a working demonstration.

**DLL reference** means adding `Assembly-CSharp.dll` (and any others you need) directly to your project. You get full IntelliSense, compile-time checks, and much simpler code. The tradeoff is that your mod will fail to compile if those DLLs change between game updates. See `DllExample` for a working demonstration.

For simple local mods or anything that only touches a few fields, reflection is fine. For larger mods that interact with many game systems, a DLL reference is usually easier to work with.

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

## 6. Host / Client Mod Matching (Compatibility Levels)

Idiots Stealing Things checks mods during the join handshake. By default, **a mod must be
installed on the host AND every client at the same version** if you join a host whose
mods you don't have (or vice-versa), you're rejected with a list of what to fix.

A mod can relax that by declaring a compatibility level. Add a single `const string`
named `ISTCompat` to your plugin class:

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
| `ServerOnly` | Only the host needs it, clients don't | Host-side economy/spawn tweaks with no client code |

Notes:
* The value is case-insensitive. `"client"` / `"server"` / `"all"` also work.
* Omitting `ISTCompat` (or a typo) is treated as `Everyone`, the safe default.
* When in doubt, leave it as `Everyone`. Only mark a mod `ClientOnly` if it genuinely
  has no effect on anyone else's game.
* **Custom maps are not mods and never touch this gate.** Missing a map doesn't stop you
  joining a lobby the game runs its own availability check once a host actually picks the
  map (see section 8).

---

## 7. Example Mods

Two example projects are included, showing the same mod written both ways.

### ReflectionExample

A local player speed boost toggled with F1. Demonstrates how to access game types and
fields at runtime without referencing any game DLLs directly. The mod compiles with
only BepInEx and UnityEngine, making it resilient to game updates.

Declared as `ClientOnly` because it only affects the local player's movement and
nothing is synced over the network.

### DllExample

The same speed boost, rewritten using a direct `Assembly-CSharp.dll` reference.
The code is simpler and you get full IntelliSense and compile-time checks, but the
mod will break if the game updates and those fields change. Compare this side-by-side
with `ReflectionExample` to understand the tradeoff.

Also declared `ClientOnly`.

---

## 8. Custom Maps

A map is **data**, not code. It ships as an AssetBundle plus a small JSON manifest sitting next
to it, with no plugin and no DLL involved:

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

The SDK's exporter writes both files for you you never hand-edit the JSON. At startup the
game scans `BepInEx/plugins` (including subfolders) for `*.istmap.json` and registers every map
it finds, then handles the rest at runtime: listing it in the lobby's Match Settings map picker,
warning players who don't have it, loading the bundle on every machine, baking the NavMesh,
turning your dummy markers into real cameras / vans / loot, and injecting all the heist managers.

| Field | Required | Purpose |
|---|---|---|
| `id` | yes | Unique, reverse-domain, **never changes** between versions. Games match maps by this. |
| `name` | no | Shown in the lobby picker. Defaults to the id. |
| `version` | no | Defaults to `1.0.0`. Players on different versions count as missing the map. |
| `bundle` | yes | Bundle filename, relative to the manifest. Must stay inside the manifest's own folder. |

A map never blocks the lobby: players without it can still join and gather. When the host selects
your map, anyone missing it sees an on-screen "MISSING MAP" notice (below the timer) and the host
can't start the round until everyone has installed it.

### Why maps don't use plugins

A BepInEx plugin is arbitrary code with full access to the machine it runs on. That's fine and
necessary for gameplay mods, but a map doesn't need any of it and if maps shipped as plugins
too, players would have no way to tell the two apart. So they don't:

> A map is a bundle and a JSON file. If something calling itself a map ships a DLL, that DLL is
> doing something other than being a map.

**Maps carry no scripts.** When the game loads a custom map it strips every behaviour component
from the scene, along with UnityEvent hooks and animation events, keeping only geometry,
colliders, lights, audio, particles, renderers and the SDK's own marker components. This is
enforced at runtime, so it holds regardless of what ends up in your bundle. The exporter warns
you at export time if your scene contains anything that will be removed. Build gameplay out of
the SDK's markers and NpcZones, and movement out of Animator/Animation.

None of this makes installing mods risk-free a gameplay mod is still code you're choosing to
run, and you should get mods from sources you trust. It just means installing a *map* isn't that.

### Building a custom map (IST_MapSDK)

Maps are built in the separate **IST_MapSDK** Unity project, not the full game project. It runs
the same Unity version (6000.x) and is preconfigured so you can start immediately: it ships the
dummy prefabs, the marker / zone scripts, and the exporter, plus an example scene at
`Project/Scenes/demomap` that demonstrates dummy placement, NpcZones, and a layout you can copy.
(The SDK mirrors the game's folder layout, so `Project/Scenes/`, `Project/Scripts/`, etc. line
up with the real project.)

1. Open `IST_MapSDK` and open `Project/Scenes/demomap` for reference, or start a new scene.
2. Build your level: import your own assets (3D models, materials, textures) and lay it out.
3. Place the dummy prefabs where things should appear in-game. Each carries an `ISTMapMarker`:

   | Dummy prefab | Becomes in-game |
   |---|---|
   | `Dummy_PlayerSpawn` | Where players spawn (place at least one, ideally least 12) |
   | `Dummy_Van_Standard` | Extraction van (place one or two) |
   | `Dummy_LootSpawn` | A random sellable loot item |
   | `Dummy_Camera` | A security camera (aimed along the object's forward arrow) |

4. Add `NpcZone` components for the crowd: **Public** zones in walkable / hallway areas
   (customers and police spawn there) and **Store** zones over shops. No zones means no NPCs.
5. *(Optional)* Add one `ISTMapConfig` to a root object to tune the loot budget.
6. Save the scene, then run **IST > Export Current Map**. Fill in your map's **ID**, **display
   name** and **version** (remembered per scene, so re-exporting is one click), then hit Export.
   It validates your markers, warns about any scripts the game will strip, excludes the default
   camera and light, and writes both files to `MapExports/`.

The bundle is named `<scene>` lower-cased + `_map` (a scene called `Demo` exports `demo_map`), and
the manifest is that name plus `.istmap.json`.

### Installing the map

Deployment is manual on purpose, so re-exporting a map is just dropping in the new files. Put
both in the plugins folder together a subfolder is fine and keeps things tidy:

```
BepInEx/plugins/
└── DemoMap/
    ├── demomap_map
    └── demomap_map.istmap.json
```

The map then appears in the lobby's Match Settings map picker on every machine that has it.

To publish on Thunderstore, package that folder as normal. No DLL, no dependency on BepInEx
beyond the folder it lives in.

---

## 9. Troubleshooting Notes

* Ensure mods match the game version
* Check `BepInEx/LogOutput.log` for errors
* Some mods require dependencies (mod managers handle this automatically)
* For maps, confirm the `bundle` value in the `.istmap.json` exactly matches the bundle file
  sitting next to it (a name mismatch logs "bundle ... not found" and is the most common reason
  a map won't load). Copy both files together; neither works alone.
* A map that loads but is missing things you placed: check `LogOutput.log` for
  `[IST.Maps] Sanitizer: removed ...` lines. Custom maps may not carry script components, and
  the SDK exporter warns about this before you export.
