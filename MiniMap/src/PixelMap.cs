using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>What a car marker on the map stands for.</summary>
	internal enum Marker
	{
		Bot,
		Remote,
	}

	/// <summary>
	/// A map as the player sees it: a small picture put together pixel by pixel each frame and shown with point filtering, so it has the
	/// hard square pixels of the game's own art. Turning a smooth picture would blur it; here each pixel of the map takes the one pixel of
	/// the <see cref="TrackImage"/> under it after the turn, and the car markers are drawn as pixel shapes on top, always upright.
	/// Round (the screen minimap, with a ring at the rim) or square (the GPS screen, whose casing is drawn round it separately).
	/// </summary>
	internal sealed class PixelMap
	{
		/// <summary>Pixels of ring at the rim of a round map: a dark outline, the frame colour, a lighter inner line.</summary>
		internal const int Ring = 4;

		internal static readonly Color32 Outline = new Color32(14, 13, 16, 255);
		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
		private static readonly Color32 PlayerFill = new Color32(245, 245, 240, 255);
		private static readonly Color32 BotFill = new Color32(232, 67, 58, 255);
		private static readonly Color32 RemoteFill = new Color32(63, 169, 245, 255);

		/// <summary>The player: an arrowhead pointing up, the way the car points. '#' outline, 'o' fill, rows from the top.</summary>
		private static readonly string[] Arrow =
		{
			"...#...",
			"..#o#..",
			".#ooo#.",
			".#ooo#.",
			"#ooooo#",
			"#oo#oo#",
			"##...##",
		};

		/// <summary>The player on a small map: the same arrowhead, smaller.</summary>
		private static readonly string[] SmallArrow =
		{
			"..#..",
			".#o#.",
			"#ooo#",
			"#o#o#",
		};

		/// <summary>Any other car: a round dot.</summary>
		private static readonly string[] Dot =
		{
			".###.",
			"#ooo#",
			"#ooo#",
			"#ooo#",
			".###.",
		};

		/// <summary>Any other car on a small map.</summary>
		private static readonly string[] SmallDot =
		{
			"###",
			"#o#",
			"###",
		};

		/// <summary>Below this width the markers are drawn small, so they do not hide the lap.</summary>
		private const int SmallBelow = 72;

		internal readonly int Size;
		internal readonly bool Round;
		/// <summary>Pixels kept clear of the lap at the rim.</summary>
		internal int Room => Round ? Ring : 1;
		/// <summary>The picture, rows from the bottom.</summary>
		internal readonly Color32[] Pixels;
		private Texture2D texture;

		internal PixelMap(int size, bool round)
		{
			Size = size;
			Round = round;
			Pixels = new Color32[size * size];
		}

		/// <summary>The texture the picture is shown on, made on first use.</summary>
		internal Texture2D Texture
		{
			get
			{
				if (texture == null)
				{
					texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
					texture.name = "MiniMap";
					texture.filterMode = FilterMode.Point;
					texture.wrapMode = TextureWrapMode.Clamp;
					texture.hideFlags = HideFlags.HideAndDontSave;
				}
				return texture;
			}
		}

		/// <summary>Sends the picture to the texture.</summary>
		internal void Upload()
		{
			Texture.SetPixels32(Pixels);
			Texture.Apply(false);
		}

		internal void Destroy()
		{
			if (texture != null)
			{
				Object.Destroy(texture);
			}
			texture = null;
		}

		/// <summary>Draws the map turned so <paramref name="heading"/> (degrees clockwise from north) points up, with the player at <paramref name="player"/>.</summary>
		internal void Draw(TrackImage image, Palette palette, float heading, Vector3 player, List<KeyValuePair<Vector3, Marker>> cars)
		{
			float rad = heading * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);
			float half = Size * 0.5f;
			int detail = TrackImage.Detail;
			int side = image.Side;
			Color32[] source = image.Pixels;
			Color32 ground = palette.Ground;
			Color32 frame = palette.Frame;
			Color32 rim = RimColour(palette);

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					int at = y * Size + x;
					if (Rim(ox, oy, frame, rim, at))
					{
						continue;
					}
					// The map turns the other way to the car: un-turn the screen point to find the map point under it.
					float mx = ox * cos + oy * sin;
					float my = -ox * sin + oy * cos;
					int sx = Mathf.FloorToInt(side * 0.5f + mx * detail);
					int sy = Mathf.FloorToInt(side * 0.5f + my * detail);
					Pixels[at] = sx < 0 || sy < 0 || sx >= side || sy >= side ? ground : source[sy * side + sx];
				}
			}

			bool small = Size < SmallBelow;
			foreach (KeyValuePair<Vector3, Marker> car in cars)
			{
				Stamp(small ? SmallDot : Dot, car.Value == Marker.Remote ? RemoteFill : BotFill, Screen(image, car.Key, cos, sin));
			}
			Stamp(small ? SmallArrow : Arrow, PlayerFill, Screen(image, player, cos, sin));
		}

		/// <summary>
		/// Draws the Open World round the player, in the middle, turned so <paramref name="heading"/> points up, <paramref name="metres"/>
		/// to a map pixel. A map pixel is wider than a tile pixel at most sizes, so it looks at a few points across itself and shows the
		/// highest ranked (see <see cref="WorldTiles"/>): roads and rivers stay unbroken however small the map. Cars out of sight are kept
		/// at the rim, so the way to another player shows; a small N at the rim points north, as there is no lap to steer by.
		/// </summary>
		internal void DrawLocal(WorldTiles world, Palette palette, float heading, float metres, Vector3 player, List<KeyValuePair<Vector3, Marker>> cars)
		{
			float rad = heading * Mathf.Deg2Rad;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);
			float half = Size * 0.5f;
			Color32 ground = palette.Ground;
			Color32 frame = palette.Frame;
			Color32 rim = RimColour(palette);
			int looks = Mathf.Clamp(Mathf.CeilToInt(metres * world.TilePixels / world.TileSize), 1, 4);

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					int at = y * Size + x;
					if (Rim(ox, oy, frame, rim, at))
					{
						continue;
					}
					// The middle decides the land; a point anywhere across the pixel can put a road or water on it.
					Color32 colour = ground;
					byte best = 0;
					if (world.Sample((ox * cos + oy * sin) * metres, (-ox * sin + oy * cos) * metres, out Color32 c, out byte r))
					{
						colour = c;
						best = r;
					}
					if (looks > 1)
					{
						for (int j = 0; j < looks; j++)
						{
							float sy = oy + (j + 0.5f) / looks - 0.5f;
							for (int i = 0; i < looks; i++)
							{
								float sx = ox + (i + 0.5f) / looks - 0.5f;
								if (world.Sample((sx * cos + sy * sin) * metres, (-sx * sin + sy * cos) * metres, out c, out r) && r > best)
								{
									colour = c;
									best = r;
								}
							}
						}
					}
					Pixels[at] = colour;
				}
			}

			bool small = Size < SmallBelow;
			float edge = Round ? half - Ring - 3f : half - 3f;
			foreach (KeyValuePair<Vector3, Marker> car in cars)
			{
				Vector2 d = new Vector2(car.Key.x - player.x, car.Key.z - player.z) / metres;
				Stamp(small ? SmallDot : Dot, car.Value == Marker.Remote ? RemoteFill : BotFill, Pixel(Pin(Turn(d, cos, sin), edge)));
			}
			Stamp(North, PlayerFill, Pixel(Pin(Turn(new Vector2(0f, 1000f), cos, sin), edge - 1f)));
			Stamp(small ? SmallArrow : Arrow, PlayerFill, Pixel(Vector2.zero));
		}

		/// <summary>The N at the rim of a local map: white on a dark plate, rows from the top.</summary>
		private static readonly string[] North =
		{
			"######",
			"#o##o#",
			"#oo#o#",
			"#o#oo#",
			"#o##o#",
			"######",
		};

		/// <summary>A point east, north of the middle in map pixels, turned with the map.</summary>
		private static Vector2 Turn(Vector2 m, float cos, float sin) => new Vector2(m.x * cos - m.y * sin, m.x * sin + m.y * cos);

		/// <summary>Brings a point from the middle back to <paramref name="edge"/> pixels out if it is further: round or square, as the map.</summary>
		private Vector2 Pin(Vector2 p, float edge)
		{
			float far = Round ? p.magnitude : Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
			return far > edge ? p * (edge / far) : p;
		}

		/// <summary>The map pixel of a point given from the middle.</summary>
		private Vector2Int Pixel(Vector2 p) => new Vector2Int(Mathf.FloorToInt(Size * 0.5f + p.x), Mathf.FloorToInt(Size * 0.5f + p.y));

		private static Color32 RimColour(Palette palette) => Color.Lerp(palette.Frame, Color.white, 0.35f);

		/// <summary>On a round map, sets the pixels outside the circle and in the ring round it and says so; the rest is the map's own.</summary>
		private bool Rim(float ox, float oy, Color32 frame, Color32 rim, int at)
		{
			if (!Round)
			{
				return false;
			}
			float half = Size * 0.5f;
			float d = Mathf.Sqrt(ox * ox + oy * oy);
			if (d > half)
			{
				Pixels[at] = Clear;
				return true;
			}
			if (d > half - Ring)
			{
				Pixels[at] = d > half - 1f ? Outline : d > half - Ring + 1f ? frame : rim;
				return true;
			}
			return false;
		}

		/// <summary>Fills the map with one colour: the GPS screen before a race has a lap to show.</summary>
		internal void Blank(Color32 colour)
		{
			for (int i = 0; i < Pixels.Length; i++)
			{
				Pixels[i] = colour;
			}
		}

		/// <summary>Where a world position is on the turned map, in its pixels from the bottom left corner.</summary>
		private Vector2Int Screen(TrackImage image, Vector3 world, float cos, float sin)
		{
			return Pixel(Turn(image.ToPixels(world) / TrackImage.Detail, cos, sin));
		}

		/// <summary>Draws a pixel shape centred on a point, inside the ring of a round map.</summary>
		private void Stamp(string[] shape, Color32 fill, Vector2Int at)
		{
			int h = shape.Length;
			float half = Size * 0.5f;
			float inside = half - Ring;
			for (int row = 0; row < h; row++)
			{
				string line = shape[row];
				int y = at.y + h / 2 - row;
				for (int col = 0; col < line.Length; col++)
				{
					char c = line[col];
					int x = at.x - line.Length / 2 + col;
					if (c == '.' || x < 0 || y < 0 || x >= Size || y >= Size)
					{
						continue;
					}
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					if (Round && ox * ox + oy * oy > inside * inside)
					{
						continue;
					}
					Pixels[y * Size + x] = c == '#' ? Outline : fill;
				}
			}
		}
	}
}
