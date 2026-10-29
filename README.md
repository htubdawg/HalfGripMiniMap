# HalfGripMiniMap
Minimap for half grip demo
100% AI generated code

![The minimap on each map and weather](docs/preview.png)

============================================================
MINIMAP  (MiniMap 0.2.0, for Half Grip Demo 0.418)
============================================================

A round, pixel-art minimap in the top left corner of the screen during races,
clear of the placement board.

  - Shows the whole lap, turned so the way your car points is always up.
  - Your car is the white arrow; bots are red dots, other players in a
    Multiplayer room are blue dots. The chequered bar is the start line.
  - Works on any map: it is drawn from the race's own track line, so the
    game's maps, ProcTrack's procedural maps and other modded maps all get
    one. If a mod replaces the track after the scene loads, the map redraws.
  - Coloured to match the map it is on:
      Stadium   grey asphalt on a dirt floor, concrete edges
      Forest    brown dirt road through green grass (red in the red forest)
      Mountain  grey road across white snow
      Factory   dark road on an ochre dirt basin, hazard orange edges
      Tokyo     dark city with a neon pink edge
    The weather shades it too: night darkens it, snow and ash lie over the
    ground. A map in a scene of its own is coloured from its own ground and
    road.
  - Drawn in square pixels in the map's colours, like the game's own art.
  - Hidden while the game is paused.

KEYS
  K   show / hide the minimap (rebind in Settings > Controls)

INSTALL
  Needs HalfGripTools 1.0.2 or newer. Copy the MiniMap folder to
  <game>\BepInEx\HalfGripMods\MiniMap\ and start the game.

SETTINGS  (BepInEx\config\halfgrip.minimap.cfg)
  Display.Shown      show the minimap (the K key flips it)
  Display.Corner     TopLeft, TopRight, BottomLeft or BottomRight
  Display.Size       width in pixels of a 1080 pixel high screen (300)
  Display.PixelSize  screen pixels per minimap pixel (3, like the game's art)
  Display.Margin     gap to the screen edges (24)
  Display.Opacity    1 is solid (0.9)
  Debug.VerboseLog   log each map build: scene, colours, lap length
