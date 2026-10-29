# HalfGripMiniMap

A Half Grip (Steam app 4163270, Unity, Windows) mod: a round minimap in a corner of the screen that shows the whole lap, turned with the
player's car, with a marker per car. Current release: see `MiniMap/mod.json` (`version`, `gameVersion`).

## How the mod runs

- A **source mod** for [HalfGripTools](https://github.com/4l3x4nd3r0/HalfGripTools) (BepInEx 5). The loader compiles `MiniMap/src/*.cs` at
  game start with Roslyn (**C# 10**, netstandard2.1) against every DLL in the game's `Managed` folder, BepInEx, 0Harmony and HalfGripTools.
  What ships is the `MiniMap/` folder; it is installed to `<game>\BepInEx\HalfGripMods\MiniMap\`. It needs HalfGripTools 1.0.2+ (`ctx.Keys`).
- Loader API: `docs/WRITING_MODS.md`; known game classes: `docs/GAME_NOTES.md` (both in HalfGripTools). Game DLLs and scene dumps are in the
  private `htubdawg/HalfGripRefs` repo.

## Layout of src/

| File | Role |
|---|---|
| `Mod.cs` | Config, the toggle key, makes the persistent HUD object |
| `MiniMapHud.cs` | Finds the race's `TrackManager`, redraws the lap when `lapManager.trackLine` changes, gathers the cars each frame, shows the texture |
| `TrackImage.cs` | The lap from above in flat palette colours (ground, road, edge lines, start line) at 4x the minimap's resolution, fitted inside the round window |
| `PixelMap.cs` | The shown texture (about 100 px, point filtered): rebuilt each frame by sampling `TrackImage` turned to the player's heading, plus the pixel ring and car markers |
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
- Work on a branch and open a PR; do not push to `main`.
- `docs/preview.png` (the README image) was rendered offline: real ProcTrack laps from its `Generator.Make` run in a console app, drawn
  by a Python port of `TrackImage` and `PixelMap`. Re-render it when the look changes.

## Checking changes

- Compile check: HalfGripTools `tools\check-mods.ps1 -ModsPath <repo> -Mod MiniMap -GameDir <HalfGripRefs>\game`. The same works on
  Linux with `dotnet build` of an equivalent netstandard2.1 / C# 10 project referencing the refs' `game` folder.
- In game: copy `MiniMap/` to `<game>\BepInEx\HalfGripMods\`, set `Debug.VerboseLog=true`, read `BepInEx\LogOutput.log`.
