using System;
using System.Collections.Generic;
using HalfGripOpenWorld;
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
	/// Draws the map the mod shows in the Open World (PixelMap.DrawLocal over WorldTiles) on real Open World tiles, made here by the
	/// Open World's own generator, round its spawn on the road into Kingsbridge:
	///   dotnet run -c Release --project tools/worldview [-p:OpenWorld=&lt;repo&gt;] [-p:Refs=&lt;HalfGripRefs&gt;] -- &lt;out.png&gt;
	/// writes one sheet: the screen minimap heading north and turned, in the town, out in the fields with a wider view, a tile not
	/// loaded yet, and the GPS screen (56 px, as on the dash at the default width), all at the default 2.5 tiles across but the fourth.
	/// Two other players stand by: one in sight, one far off and kept at the rim. The repos default to siblings of this one.
	/// </summary>
	internal static class Program
	{
		private const float TileSize = 250f;
		private const int Pixels = 64;

		private static readonly Dictionary<long, Color32[]> made = new Dictionary<long, Color32[]>();
		private static bool holes;

		private static readonly Color32 MapWater = new Color32(46, 82, 128, 255);
		private static readonly Color32[] MapRoads =
		{
			new Color32(225, 110, 40, 255),
			new Color32(235, 190, 60, 255),
			new Color32(240, 225, 130, 255),
			new Color32(70, 70, 74, 255),
			new Color32(238, 236, 228, 255),
			new Color32(150, 110, 70, 255),
		};

		/// <summary>
		/// A tile's map picture, as the Open World's Streamer.Colours and MapPicture make it (a copy: those are in the Unity side of the
		/// mod). The land's height is read straight from WorldGen rather than from the tile's height grid, which only moves a few edges.
		/// </summary>
		private static Color32[] Tile(int tx, int tz)
		{
			long key = ((long)tx << 32) ^ (uint)tz;
			if (made.TryGetValue(key, out Color32[] map))
			{
				return map;
			}
			map = new Color32[Pixels * Pixels];
			double pixel = TileSize / (double)Pixels;
			double ox = tx * (double)TileSize, oz = tz * (double)TileSize;
			for (int k = 0; k < map.Length; k++)
			{
				double wx = ox + (k % Pixels + 0.5) * pixel, wz = oz + (k / Pixels + 0.5) * pixel;
				double h = WorldGen.Height(wx, wz);
				double sx = (WorldGen.Height(wx + 2, wz) - WorldGen.Height(wx - 2, wz)) / 4;
				double sz = (WorldGen.Height(wx, wz + 2) - WorldGen.Height(wx, wz - 2)) / 4;
				Cover cover = LandUse.Classify(wx, wz, h - Network.Lift, Math.Sqrt(sx * sx + sz * sz), out int crop);
				LandUse.Rgb rgb = LandUse.Colour(cover, crop, wx, wz);
				map[k] = new Color32(rgb.R, rgb.G, rgb.B, 255);
				if (Rivers.Water(wx, wz, out double level) && level > h - Network.Lift)
				{
					map[k] = MapWater;
				}
			}
			List<long> pieces = new List<long>();
			for (int j = tz - 1; j <= tz + 1; j++)
			{
				for (int i = tx - 1; i <= tx + 1; i++)
				{
					List<long> list = Network.PiecesIn(i, j, TileSize);
					if (list != null)
					{
						pieces.AddRange(list);
					}
				}
			}
			for (int kind = MapRoads.Length - 1; kind >= 0; kind--)
			{
				foreach (long piece in pieces)
				{
					Network.Road road = Network.Roads[(int)(piece >> 32)];
					if ((int)road.Kind != kind)
					{
						continue;
					}
					int k = (int)(uint)piece;
					double reach = Math.Max(road.Half, pixel);
					int p0 = Math.Max(0, (int)Math.Floor((Math.Min(road.X[k], road.X[k + 1]) - reach - ox) / pixel));
					int p1 = Math.Min(Pixels - 1, (int)Math.Floor((Math.Max(road.X[k], road.X[k + 1]) + reach - ox) / pixel));
					int q0 = Math.Max(0, (int)Math.Floor((Math.Min(road.Z[k], road.Z[k + 1]) - reach - oz) / pixel));
					int q1 = Math.Min(Pixels - 1, (int)Math.Floor((Math.Max(road.Z[k], road.Z[k + 1]) + reach - oz) / pixel));
					for (int q = q0; q <= q1; q++)
					{
						for (int p = p0; p <= p1; p++)
						{
							Lines.Project(road.X, road.Z, k, ox + (p + 0.5) * pixel, oz + (q + 0.5) * pixel, out double _, out double d);
							if (d <= reach)
							{
								map[q * Pixels + p] = MapRoads[kind];
							}
						}
					}
				}
			}
			made[key] = map;
			return map;
		}

		/// <summary>The stand-in for MapView.TryGetTile; with <see cref="holes"/> one tile near the spawn is not loaded yet.</summary>
		private static bool Fetch(int tx, int tz, Color32[] into)
		{
			if (holes && tx == -13 && tz == -25)
			{
				return false;
			}
			Array.Copy(Tile(tx, tz), into, Pixels * Pixels);
			return true;
		}

		private static PixelMap Draw(double x, double z, float heading, int size, bool round, float viewTiles, List<KeyValuePair<Vector3, Marker>> cars)
		{
			WorldTiles tiles = new WorldTiles();
			tiles.Window(x, z, viewTiles * TileSize * 0.75f + 10f, Fetch);
			PixelMap map = new PixelMap(size, round);
			// As Race.MetresPerPixel.
			float metres = viewTiles * TileSize / Math.Max(8, size - 2 * map.Room);
			map.DrawLocal(tiles, Palettes.OpenWorld, heading, metres, Vector3.zero, cars);
			return map;
		}

		private static int Main(string[] args)
		{
			string path = args.Length > 0 ? args[0] : "openworld.png";
			Network.Ensure();
			double x = Network.SpawnX, z = Network.SpawnZ;
			List<KeyValuePair<Vector3, Marker>> cars = new List<KeyValuePair<Vector3, Marker>>
			{
				new KeyValuePair<Vector3, Marker>(new Vector3(60f, 0f, 80f), Marker.Remote),
				new KeyValuePair<Vector3, Marker>(new Vector3(-2000f, 0f, 900f), Marker.Remote),
			};
			List<PixelMap> maps = new List<PixelMap>
			{
				Draw(x, z, 0f, 100, true, 2.5f, cars),
				Draw(x, z, 35f, 100, true, 2.5f, cars),
				Draw(x + 600, z + 300, 300f, 100, true, 2.5f, cars),
				Draw(x + 3000, z + 2500, 10f, 100, true, 4f, cars),
			};
			holes = true;
			maps.Add(Draw(x, z, 0f, 100, true, 2.5f, cars));
			holes = false;
			maps.Add(Draw(x, z, 120f, 56, false, 2.5f, cars));

			// Side by side at 3 screen pixels a map pixel (the default Display.PixelSize); the GPS at twice that so it reads.
			const int gap = 8;
			int width = gap, height = 0;
			foreach (PixelMap m in maps)
			{
				int s = m.Round ? 3 : 6;
				width += m.Size * s + gap;
				height = Math.Max(height, m.Size * s + 2 * gap);
			}
			Color32[] sheet = new Color32[width * height];
			for (int i = 0; i < sheet.Length; i++)
			{
				sheet[i] = new Color32(52, 54, 58, 255);
			}
			int left = gap;
			foreach (PixelMap m in maps)
			{
				int s = m.Round ? 3 : 6;
				int bottom = (height - m.Size * s) / 2;
				for (int y = 0; y < m.Size * s; y++)
				{
					for (int xx = 0; xx < m.Size * s; xx++)
					{
						Color32 c = m.Pixels[(y / s) * m.Size + xx / s];
						if (c.a > 0)
						{
							sheet[(bottom + y) * width + left + xx] = c;
						}
					}
				}
				left += m.Size * s + gap;
			}
			Png.Save(path, sheet, width, height);
			Console.WriteLine("Wrote " + path + " (" + width + "x" + height + ")");
			return 0;
		}
	}
}
