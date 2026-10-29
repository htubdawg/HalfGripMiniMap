using BepInEx.Configuration;
using BepInEx.Logging;
using HalfGripTools;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// A round minimap in a corner of the screen: the whole lap, turned so the way the player's car points is always up, with a marker for
	/// every car in the race. It is drawn from the race's own track line (the line the lap counter and the bots follow), not from anything
	/// known about one map, so it works on the game's maps, on ProcTrack's laps and on any other map a mod adds. The colours are chosen from
	/// the scene the race is in (see <see cref="Palettes"/>).
	/// </summary>
	public class Mod : HalfGripMod
	{
		internal static ManualLogSource Log;

		internal static ConfigEntry<bool> Shown;
		internal static ConfigEntry<string> Corner;
		internal static ConfigEntry<int> Size;
		internal static ConfigEntry<int> Margin;
		internal static ConfigEntry<int> PixelSize;
		internal static ConfigEntry<float> Opacity;
		internal static ConfigEntry<bool> Verbose;
		internal static ModKey Toggle;

		private static GameObject hud;

		/// <summary>A detail line for the log: only written when Debug.VerboseLog is on. Warnings and errors are always written.</summary>
		internal static void Info(string text)
		{
			if (Verbose != null && Verbose.Value)
			{
				Log.LogInfo(text);
			}
		}

		/// <summary>
		/// BepInEx keeps a setting's saved value when its default changes, so a default that moves is moved here once for configs written
		/// by an older version. 2 (0.2.0): the minimap went from the top right to the top left, off the placement board.
		/// </summary>
		private static void Migrate(ConfigFile config)
		{
			ConfigEntry<int> version = config.Bind("Debug", "ConfigVersion", 1, "Which version of the mod last updated this file. Leave it alone.");
			if (version.Value < 2)
			{
				if (Corner.Value == "TopRight")
				{
					Corner.Value = "TopLeft";
				}
				version.Value = 2;
			}
		}

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;

			Shown = ctx.Config.Bind("Display", "Shown", true, "Show the minimap in races. The toggle key (Settings > Controls, K by default) flips this.");
			Corner = ctx.Config.Bind("Display", "Corner", "TopLeft", new ConfigDescription("The corner of the screen the minimap sits in. The race's placement board is in the top right.", new AcceptableValueList<string>("TopLeft", "TopRight", "BottomLeft", "BottomRight")));
			Size = ctx.Config.Bind("Display", "Size", 300, new ConfigDescription("Width of the minimap, in pixels of a 1080 pixel high screen (it scales with the screen).", new AcceptableValueRange<int>(120, 700)));
			Margin = ctx.Config.Bind("Display", "Margin", 24, new ConfigDescription("Gap between the minimap and the edges of the screen, in pixels of a 1080 pixel high screen.", new AcceptableValueRange<int>(0, 200)));
			PixelSize = ctx.Config.Bind("Display", "PixelSize", 3, new ConfigDescription("Screen pixels (of a 1080 pixel high screen) per pixel of the minimap. 3 matches the chunky pixels of the game's own art; 1 is the finest.", new AcceptableValueRange<int>(1, 8)));
			Opacity = ctx.Config.Bind("Display", "Opacity", 0.9f, new ConfigDescription("How opaque the minimap is: 1 is solid.", new AcceptableValueRange<float>(0.2f, 1f)));
			Verbose = ctx.Config.Bind("Debug", "VerboseLog", false, "Write each map build (the scene, the palette, the size of the lap) to the BepInEx log.");
			Migrate(ctx.Config);

			Toggle = ctx.Keys.Add(KeyCode.K, "Show / hide minimap", Controls.Section.Race);
			if (Toggle == null)
			{
				Log.LogWarning("The minimap's toggle key K belongs to the game or another mod; the minimap can still be switched in the config (Display.Shown).");
			}

			hud = new GameObject("MiniMapHud");
			Object.DontDestroyOnLoad(hud);
			MiniMapHud runner = hud.AddComponent<MiniMapHud>();
			ctx.OnSceneLoaded(runner.OnSceneLoaded);
			ctx.OnUnload(() =>
			{
				if (hud != null)
				{
					Object.Destroy(hud);
				}
				hud = null;
			});
		}
	}
}
