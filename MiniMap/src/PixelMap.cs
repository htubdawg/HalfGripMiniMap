using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>What a car marker on the minimap stands for.</summary>
	internal enum Marker
	{
		Bot,
		Remote,
	}

	/// <summary>
	/// The minimap as the player sees it: a small texture, put together pixel by pixel each frame and shown with point filtering, so it
	/// has the hard square pixels of the game's own art. Turning a smooth picture would blur it; here each pixel of the minimap takes
	/// the one pixel of the <see cref="TrackImage"/> under it after the turn, and the frame and car markers are drawn as pixel shapes on
	/// top, always upright.
	/// </summary>
	internal sealed class PixelMap
	{
		/// <summary>Pixels of ring at the rim: a dark outline, the frame colour, a lighter inner line.</summary>
		internal const int Ring = 4;

		private static readonly Color32 Outline = new Color32(14, 13, 16, 255);
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

		/// <summary>Any other car: a round dot.</summary>
		private static readonly string[] Dot =
		{
			".###.",
			"#ooo#",
			"#ooo#",
			"#ooo#",
			".###.",
		};

		internal readonly int Size;
		internal readonly Texture2D Texture;
		private readonly Color32[] pixels;

		internal PixelMap(int size)
		{
			Size = size;
			pixels = new Color32[size * size];
			Texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
			Texture.name = "MiniMap";
			Texture.filterMode = FilterMode.Point;
			Texture.wrapMode = TextureWrapMode.Clamp;
			Texture.hideFlags = HideFlags.HideAndDontSave;
		}

		/// <summary>Draws the minimap turned so <paramref name="heading"/> (degrees clockwise from north) points up, with the player at <paramref name="player"/>.</summary>
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
					float d = Mathf.Sqrt(ox * ox + oy * oy);
					int at = y * Size + x;
					if (d > half)
					{
						pixels[at] = Clear;
						continue;
					}
					if (d > half - Ring)
					{
						pixels[at] = d > half - 1f ? Outline : d > half - Ring + 1f ? frame : rim;
						continue;
					}
					// The map turns the other way to the car: un-turn the screen point to find the map point under it.
					float mx = ox * cos + oy * sin;
					float my = -ox * sin + oy * cos;
					int sx = Mathf.FloorToInt(side * 0.5f + mx * detail);
					int sy = Mathf.FloorToInt(side * 0.5f + my * detail);
					pixels[at] = sx < 0 || sy < 0 || sx >= side || sy >= side ? ground : source[sy * side + sx];
				}
			}

			foreach (KeyValuePair<Vector3, Marker> car in cars)
			{
				Stamp(Dot, car.Value == Marker.Remote ? RemoteFill : BotFill, Screen(image, car.Key, cos, sin));
			}
			Stamp(Arrow, PlayerFill, Screen(image, player, cos, sin));

			Texture.SetPixels32(pixels);
			Texture.Apply(false);
		}

		/// <summary>Where a world position is on the turned minimap, in its pixels from the bottom left corner.</summary>
		private Vector2Int Screen(TrackImage image, Vector3 world, float cos, float sin)
		{
			Vector2 m = image.ToPixels(world) / TrackImage.Detail;
			float x = m.x * cos - m.y * sin;
			float y = m.x * sin + m.y * cos;
			return new Vector2Int(Mathf.FloorToInt(Size * 0.5f + x), Mathf.FloorToInt(Size * 0.5f + y));
		}

		/// <summary>Draws a pixel shape centred on a point, inside the ring only.</summary>
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
					if (ox * ox + oy * oy > inside * inside)
					{
						continue;
					}
					pixels[y * Size + x] = c == '#' ? Outline : fill;
				}
			}
		}
	}
}
