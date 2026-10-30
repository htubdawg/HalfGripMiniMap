using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>What the drawing code logs through, outside the game.</summary>
	internal static class Mod
	{
		internal sealed class Logger
		{
			public void LogWarning(string text) => Console.Error.WriteLine(text);
			public void LogInfo(string text) { }
		}

		internal static Logger Log = new Logger();
		internal static void Info(string text) { }
	}

	/// <summary>
	/// Draws the mod's pictures with the mod's own drawing code (GpsArt, PixelMap, TrackImage, Palettes, which work on plain pixel
	/// arrays), outside the game, on real ProcTrack laps from laps/ (made by tools/lapdump):
	///   dotnet run -c Release --project tools/preview [-p:Refs=&lt;HalfGripRefs&gt;] -- &lt;out dir&gt;
	/// writes
	///   gps-dash.png  the GPS as on the dash (38 casing pixels wide, the screen at twice that resolution), 4x
	///   gps-icon.png  the shop icon (26x24, as Gps.IconWidth/IconHeight), 6x
	///   icon.png      the mod's picture for the mod manager, 128x128: copy it to MiniMap/icon.png
	///   gps-zoom.png  the GPS screen at its three zooms in the order a right click steps (2x, whole lap, 4x), then a tap on it 0, 0.1, 0.2 and 0.35 s on, 4x
	/// and checks that the shop icon is not blank. docs/gps.png was put together from the first two.
	/// </summary>
	internal static class Program
	{
		/// <summary>The shop icon's size: keep in step with Gps.IconWidth and Gps.IconHeight.</summary>
		private const int IconWidth = 26;
		private const int IconHeight = 24;

		private static int Main(string[] args)
		{
			string dir = args.Length > 0 ? args[0] : "preview-out";
			Directory.CreateDirectory(dir);
			string laps = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "laps");
			if (!Directory.Exists(laps))
			{
				laps = Path.Combine("tools", "preview", "laps");
			}

			DashGps(Lap(laps, 72), Path.Combine(dir, "gps-dash.png"));
			Color32[] icon = GpsArt.Icon(IconWidth, IconHeight);
			Png.Save(Path.Combine(dir, "gps-icon.png"), icon, IconWidth, IconHeight, 6);
			ModIcon(Lap(laps, 23), Path.Combine(dir, "icon.png"));
			Zooms(Lap(laps, 72), Path.Combine(dir, "gps-zoom.png"));

			int drawn = 0;
			foreach (Color32 c in icon)
			{
				drawn += c.a > 0 ? 1 : 0;
			}
			Console.WriteLine("shop icon " + IconWidth + "x" + IconHeight + ": " + drawn + " pixels drawn");
			Console.WriteLine("written to " + Path.GetFullPath(dir));
			return drawn > IconWidth * IconHeight / 3 ? 0 : 1;
		}

		private static List<Vector3> Lap(string dir, int seed)
		{
			List<Vector3> pts = new List<Vector3>();
			foreach (string line in File.ReadAllLines(Path.Combine(dir, "lap_" + seed + ".csv")))
			{
				string[] p = line.Split(',');
				pts.Add(new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), 0f, float.Parse(p[1], CultureInfo.InvariantCulture)));
			}
			return pts;
		}

		/// <summary>The lap on a square screen <paramref name="screen"/> pixels wide, the player at point <paramref name="at"/>, three bots and a Multiplayer car.</summary>
		private static PixelMap Screen(List<Vector3> pts, int screen, int at, int zoom = 1)
		{
			PixelMap map = new PixelMap(screen, false);
			TrackImage img = TrackImage.Build(pts, Palettes.Forest, screen, map.Room, zoom);
			Vector3 f = pts[at + 1] - pts[at - 1];
			float heading = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
			var cars = new List<KeyValuePair<Vector3, Marker>>
			{
				new(pts[at + 9], Marker.Bot), new(pts[at - 14], Marker.Bot), new(pts[(at + 170) % pts.Count], Marker.Bot),
				new(pts[(at + 330) % pts.Count], Marker.Remote),
			};
			map.Draw(img, Palettes.Forest, heading, pts[at], cars);
			return map;
		}

		/// <summary>The casing at the dash's style and the screen at twice its resolution, as GpsScreen puts them together.</summary>
		private static void DashGps(List<Vector3> pts, string path)
		{
			const int width = 38;
			int hole = width - GpsArt.Dash.ExtraWidth;
			GpsArt.Casing casing = GpsArt.Draw(hole, hole, GpsArt.Dash);
			PixelMap map = Screen(pts, hole * 2, 260);
			Color32[] px = Compose(casing, map, 2, out int w, out int h);
			Png.Save(path, px, w, h, 4);
		}

		/// <summary>The dash GPS's screen at zooms 2 (the first view), 1 and 4, then at zoom 1 at four moments of a tap, side by side with a gap of 2.</summary>
		private static void Zooms(List<Vector3> pts, string path)
		{
			int screen = (38 - GpsArt.Dash.ExtraWidth) * 2;
			List<PixelMap> maps = new List<PixelMap>();
			foreach (int zoom in new[] { 2, 1, 4 })
			{
				maps.Add(Screen(pts, screen, 260, zoom));
			}
			foreach (float age in new[] { 0f, 0.1f, 0.2f, 0.35f })
			{
				PixelMap map = Screen(pts, screen, 260);
				map.Tap(age);
				maps.Add(map);
			}
			int w = maps.Count * (screen + 2) - 2;
			Color32[] px = new Color32[w * screen];
			for (int i = 0; i < maps.Count; i++)
			{
				for (int y = 0; y < screen; y++)
				{
					for (int x = 0; x < screen; x++)
					{
						px[y * w + i * (screen + 2) + x] = maps[i].Pixels[y * screen + x];
					}
				}
			}
			Png.Save(path, px, w, screen, 4);
		}

		/// <summary>The mod manager's picture: the GPS in its card style, 4 pixels per casing pixel, a lap on its screen at 2 per pixel.</summary>
		private static void ModIcon(List<Vector3> pts, string path)
		{
			const int side = 128;
			const int hole = 19;
			GpsArt.Casing casing = GpsArt.Draw(hole, hole, GpsArt.Card);
			PixelMap map = Screen(pts, hole * 2, 200);
			Color32[] dev = Compose(casing, map, 2, out int w, out int h);
			Color32[] px = new Color32[side * side];
			int ox = (side - w * 2) / 2, oy = (side - h * 2) / 2;
			for (int y = 0; y < h * 2; y++)
			{
				for (int x = 0; x < w * 2; x++)
				{
					px[(oy + y) * side + ox + x] = dev[(y / 2) * w + x / 2];
				}
			}
			Png.Save(path, px, side, side);
		}

		/// <summary>The casing scaled up by <paramref name="detail"/> with the screen's pixels in its hole, one each.</summary>
		private static Color32[] Compose(GpsArt.Casing casing, PixelMap map, int detail, out int w, out int h)
		{
			w = casing.Width * detail;
			h = casing.Height * detail;
			Color32[] px = new Color32[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					px[y * w + x] = casing.Pixels[(y / detail) * casing.Width + x / detail];
				}
			}
			for (int y = 0; y < map.Size; y++)
			{
				for (int x = 0; x < map.Size; x++)
				{
					px[(casing.ScreenY * detail + y) * w + casing.ScreenX * detail + x] = map.Pixels[y * map.Size + x];
				}
			}
			return px;
		}
	}
}
