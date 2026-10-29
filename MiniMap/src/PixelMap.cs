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
			Color32 rim = Color.Lerp(palette.Frame, Color.white, 0.35f);

			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					float ox = x + 0.5f - half;
					float oy = y + 0.5f - half;
					int at = y * Size + x;
					if (Round)
					{
						float d = Mathf.Sqrt(ox * ox + oy * oy);
						if (d > half)
						{
							Pixels[at] = Clear;
							continue;
						}
						if (d > half - Ring)
						{
							Pixels[at] = d > half - 1f ? Outline : d > half - Ring + 1f ? frame : rim;
							continue;
						}
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
			Vector2 m = image.ToPixels(world) / TrackImage.Detail;
			float x = m.x * cos - m.y * sin;
			float y = m.x * sin + m.y * cos;
			return new Vector2Int(Mathf.FloorToInt(Size * 0.5f + x), Mathf.FloorToInt(Size * 0.5f + y));
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
