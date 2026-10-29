# HalfGripMiniMap

A Half Grip (Steam app 4163270, Unity, Windows) mod: a round minimap in a corner of the screen that shows the whole lap, turned with the
player's car, with a marker per car; and a GPS shop item that shows the same map on a screen on the dash. Current release: see `MiniMap/mod.json` (`version`, `gameVersion`).

## How the mod runs

- A **source mod** for [HalfGripTools](https://github.com/4l3x4nd3r0/HalfGripTools) (BepInEx 5). The loader compiles `MiniMap/src/*.cs` at
  game start with Roslyn (**C# 10**, netstandard2.1) against every DLL in the game's `Managed` folder, BepInEx, 0Harmony and HalfGripTools.
  What ships is the `MiniMap/` folder; it is installed to `<game>\BepInEx\HalfGripMods\MiniMap\`. It needs HalfGripTools 1.0.2+ (`ctx.Keys`).
- Loader API: `docs/WRITING_MODS.md`; known game classes: `docs/GAME_NOTES.md` (both in HalfGripTools). Game DLLs and scene dumps are in the
  private `htubdawg/HalfGripRefs` repo.

## Layout of src/

| File | Role |
|---|---|
| `Mod.cs` | Config, the toggle key, registers the GPS, makes the persistent HUD object |
| `Race.cs` | Shared by both maps: finds the race's `TrackManager`, bumps `Version` when `lapManager.trackLine` or the colours change, gathers the cars each frame; `LapCache` keeps each map's `TrackImage` |
| `MiniMapHud.cs` | The screen minimap: a round `PixelMap` on an overlay canvas in a corner |
| `Gps.cs` | The GPS item: a clone of the magnet item (no code of its own, MEDIUM, module mount) with its exterior and stats stripped, a new interior prefab and a drawn icon; measures the dash's pixel size from the magnet's sprites |
| `GpsScreen.cs` | The GPS on the dash (made by the game's `LoadoutSpawner` from the prefab): a lit casing sprite and an unlit screen sprite showing a square `PixelMap` |
| `GpsArt.cs` | The GPS casing and shop icon, drawn as pixel arrays |
| `TrackImage.cs` | The lap from above in flat palette colours (ground, road, edge lines, start line) at 4x the minimap's resolution, fitted inside the round window |
| `PixelMap.cs` | A shown map (point filtered, round or square): rebuilt each frame by sampling `TrackImage` turned to the player's heading, plus the ring and car markers |
| `Palettes.cs` | Colours per base scene (from the scene name), shaded by the weather (`WeatherGrip.Current`) |
| `Sampler.cs` | Colours for unknown scenes, read off the ground under and beside the track line (terrain layers or mesh materials) |

## Rules that matter

- **Any map.** Never depend on one map's objects: the track line (`PathManager.sampledPoints`, a closed loop) and the leaderboard's cars are
  all the map needs. ProcTrack replaces the line after the scene loads, which is why the HUD re-checks it twice a second.
- The base map is told from the **scene** name, not the run map entry, so mods that reuse a game scene get that scene's colours.
- Bump `version` in `mod.json` with each release and keep the version in `README.md` in step.
- C# 10 and netstandard2.1 only. Tabs, `///` summaries that explain *why*, `Mod.Info(...)` for verbose logging, `Mod.Log.LogWarning` with a
  try/catch around risky work.
- **Pixel look.** The minimap matches the game's pixel art: hard-edged palette colours only, point filtering, a whole number of screen
  pixels per map pixel. Do not add smoothing, blending or rotated smooth sprites.
- A changed default needs `Mod.Migrate` (BepInEx keeps saved values): bump `Debug.ConfigVersion` and move old values once.
- **Release:** `Display.Shown` goes to false by default before release (the GPS is then how a player gets a map), with a `Mod.Migrate` step.
- The GPS's item id `minimap.gps` is saved in runs: never change it.
- Work on a branch and open a PR; do not push to `main`.
- `docs/preview.png` (the README image) was rendered offline: real ProcTrack laps from its `Generator.Make` run in a console app, drawn
  by a Python port of `TrackImage` and `PixelMap`. Re-render it when the look changes. `docs/gps.png` was made by compiling the mod's
  own `GpsArt`, `PixelMap`, `TrackImage` and `Palettes` into a console app against `UnityEngine.CoreModule` (their drawing is plain
  pixel arrays) and running it on a ProcTrack lap.

## Checking changes

- Compile check: HalfGripTools `tools\check-mods.ps1 -ModsPath <repo> -Mod MiniMap -GameDir <HalfGripRefs>\game`. The same works on
  Linux with `dotnet build` of an equivalent netstandard2.1 / C# 10 project referencing the refs' `game` folder.
- In game: copy `MiniMap/` to `<game>\BepInEx\HalfGripMods\`, set `Debug.VerboseLog=true`, read `BepInEx\LogOutput.log`.
