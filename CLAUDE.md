# HalfGripMiniMap

A Half Grip (Steam app 4163270, Unity, Windows) mod: a round minimap in a corner of the screen that shows the whole lap, turned with the
player's car, with a marker per car (in the Open World mod, the country round the car instead); and a GPS shop item that shows the same map on a screen on the dash. Current release: see `MiniMap/mod.json` (`version`, `gameVersion`).

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
| `Gps.cs` | The GPS item: a clone of the magnet item (no code of its own, MEDIUM, module mount) with its exterior and stats stripped, a new interior prefab and a drawn icon |
| `GpsScreen.cs` | The GPS on the dash (made by the game's `LoadoutSpawner` from the prefab): measures the cockpit's pixel size (median over the dash's sprites, about 0.05 units), then a casing sprite at that size and a square `PixelMap` screen at twice its resolution, the screen in the unlit sprite material, the casing in its own copy of the lit `SpriteLit` with its texture set (the Lit shader reads the material's `_BaseMap`, so each dash sprite has its own copy) |
| `GpsArt.cs` | The GPS casing (grey speckled plastic box with top and side faces on a base plate, after the jumper's key cap) and shop icon, drawn as pixel arrays |
| `TrackImage.cs` | The lap from above in flat palette colours (ground, road, edge lines, start line) at 4x the minimap's resolution, fitted inside the round window |
| `PixelMap.cs` | A shown map (point filtered, round or square): rebuilt each frame by sampling `TrackImage` turned to the player's heading (`DrawLocal`: the Open World's tiles round the car), plus the ring and car markers |
| `Palettes.cs` | Colours per base scene (from the scene name), shaded by the weather (`WeatherGrip.Current`) |
| `OpenWorldLink.cs` | The link to the Open World mod's `HalfGripOpenWorld.MapView`, found by name at each scene load until found, its members made into delegates; keeps the tiles round the car up to date each frame |
| `WorldTiles.cs` | The Open World's tile pictures round the car in world coordinates (scene position + origin offset), with a rank per pixel (water, then roads by class) so a shrunk map keeps them |
| `Sampler.cs` | Colours for unknown scenes, read off the ground under and beside the track line (terrain layers or mesh materials) |

## Rules that matter

- **Any map.** Never depend on one map's objects: the track line (`PathManager.sampledPoints`, a closed loop) and the leaderboard's cars are
  all the map needs. ProcTrack replaces the line after the scene loads, which is why the HUD re-checks it twice a second.
- The base map is told from the **scene** name, not the run map entry, so mods that reuse a game scene get that scene's colours.
- Bump `version` in `mod.json` with each release and keep the version at the top of `README.md` in step.
- C# 10 and netstandard2.1 only. Tabs, `///` summaries that explain *why*, `Mod.Log.LogWarning` with a try/catch around risky work.
- **Logging:** everything but warnings and errors goes through `Mod.Info(...)`, which writes only with `Debug.VerboseLog` on. No
  unconditional `LogInfo`, not even for diagnostics; ask the owner to turn verbose on instead.
- **Pixel look.** The minimap matches the game's pixel art: hard-edged palette colours only, point filtering, a whole number of screen
  pixels per map pixel. Do not add smoothing, blending or rotated smooth sprites.
- A changed default needs `Mod.Migrate` (BepInEx keeps saved values): bump `Debug.ConfigVersion` and move old values once.
- The screen minimap is opt-in since 1.0.0: `Display.Enabled` (default false, read at start) turns it on and is the only thing that adds
  its K key to Settings > Controls. K hides and shows it for the session only (not saved). The GPS is how a player gets a map by
  default.
- Keep the README's install section clear that only the `MiniMap` folder goes into `HalfGripMods\` (`mod.json` directly inside
  `HalfGripMods\MiniMap\`), not the whole repo download.
- The GPS's item id `minimap.gps` is saved in runs: never change it.
- `MiniMap/icon.png` is the mod's picture (`"icon"` in `mod.json`, shown by the HalfGripTools 1.0.5+ mod manager and by installers):
  128x128, the GPS in its card style with a ProcTrack lap on its screen, made by `tools/preview` from the mod's own `GpsArt` and
  `PixelMap`. Older loaders ignore the field, so `tools` stays 1.0.2.
- Work on a branch and open a PR; do not push to `main`.

## Checking changes

The tools expect the other repos as siblings of this one (override with the arguments or `-p:` properties shown):
`../halfgriprefs` (game DLLs, scene dumps), `../ProcTrack` (only for `tools/lapdump`), `../halfgripopenworld` (only for
`tools/worldview`), and HalfGripTools anywhere.

- **Compile check** (run before every push): build HalfGripTools once against the refs, then compile the mod:
  ```
  dotnet build <HalfGripTools>/HalfGripTools/HalfGripTools.csproj -c Release -p:GameDir=<halfgriprefs>/game
  tools/check.sh <halfgriprefs>/game <HalfGripTools>/dist/BepInEx/plugins/HalfGripTools/HalfGripTools.dll
  ```
  On Windows, HalfGripTools' `tools\check-mods.ps1 -ModsPath <repo> -Mod MiniMap -GameDir <halfgriprefs>\game` does the same.
- **Pictures** (`tools/preview`): `dotnet run -c Release --project tools/preview -- <out dir>` compiles the mod's own `GpsArt`,
  `PixelMap`, `TrackImage`, `Palettes` (plain pixel arrays) against `UnityEngine.CoreModule` and writes `gps-dash.png` (the GPS as on
  the dash), `gps-icon.png` (the shop icon; exits 1 if it is blank) and `icon.png` (copy to `MiniMap/icon.png`; it reproduces the
  committed one exactly). `docs/gps.png` was put together from the first two. Look at them after any art change.
- **README sheet:** `python3 tools/preview/minimap_sheet.py tools/preview/laps docs/preview.png` draws the round minimap on every base
  map and weather. It is a Python port of the round map (as of 0.2.0; it reproduces `docs/preview.png` exactly): keep it in step.
- **Laps:** `tools/preview/laps/` holds real ProcTrack laps (x,z CSV). `dotnet run -c Release --project tools/lapdump -- <dir> <seed>...`
  makes more from ProcTrack's own `Generator.Make`.
- **Game code:** decompile with `ilspycmd -p -o <dir> <halfgriprefs>/game/HalfGrip_Data/Managed/Assembly-CSharp.dll`
  (`dotnet tool install -g ilspycmd`; set `DOTNET_ROLL_FORWARD=Major` on a newer .NET).
- **In game** (the owner tests; ask for screenshots and the log): copy `MiniMap/` to `<game>\BepInEx\HalfGripMods\`, set
  `Debug.VerboseLog=true`, read `BepInEx\LogOutput.log`.

## Open World mode (1.1.0)

The Open World mod (`htubdawg/HalfGripOpenWorld`, 0.4.6+) is a 32 km world with a floating origin; its track line is only a token
loop. While its `MapView.Active` is true, `Race.OpenWorld` is set and both maps call `PixelMap.DrawLocal` instead of drawing the lap:

- A **local map round the car**, heading-up like the race maps, `OpenWorld.ViewTiles` (default 2.5) 250 m tiles across the inside of
  the rim (`Race.MetresPerPixel`), from the Open World's own tile pictures. No start line; a white N at the rim points north; other
  cars out of sight are pinned to the rim. The `Palettes.OpenWorld` palette gives only the rim and the ground of unloaded tiles.
- Each map pixel takes the highest ranked tile pixel among a few points across it (`WorldTiles.Ranked`: water, then roads by class,
  the colours of Open World's `docs/world.png`), so 2-pixel roads survive the GPS's ~12 m pixels. Keep `Ranked` in step if Open World
  changes its map colours; an unknown colour only ranks as land.
- **World coordinates** (scene position + origin offset) for the tile window, so origin shifts do not matter; car markers relative
  to the player.
- **Open World's public API** (`OpenWorld/src/MapView.cs`), read by **reflection by name** (each mod is compiled on its own, so MiniMap
  cannot reference it). All static properties except the one method: `bool Active`, `double OriginX`/`OriginZ`, `float TileSize`
  (250), `int TilePixels` (64), `int Version` (tiles added or removed), `bool TryGetTile(int tx, int tz, Color32[] into)` (rows from
  the south, x east; main thread only). A MapView missing a member logs one warning and the Open World gets no map.
- **Picture:** `dotnet run -c Release --project tools/worldview -- docs/openworld.png` (needs `../halfgripopenworld`, override with
  `-p:OpenWorld=`) draws the README's sheet with `DrawLocal` on tiles made by Open World's own generator round its spawn (a copy of its
  `Streamer.MapPicture`). Look at it after any change to the local map.
- Not done, still open with the owner: zooming out with speed, and a north-up choice (the player's arrow would then have to turn).
