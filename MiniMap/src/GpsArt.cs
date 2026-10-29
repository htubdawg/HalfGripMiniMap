using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The GPS's pixel art, drawn in code so it fits whatever size the dash and the shop card need, in the manner of the other items on
	/// the dash (the jumper's key cap above all): a chunky grey plastic box seen a little from above and to the left, so its top and right
	/// side show, with a speckled surface, a recessed screen, a green light and two buttons on its chin, standing on a dark base plate.
	/// Pixel arrays only, rows from the bottom; the caller makes the textures.
	/// </summary>
	internal static class GpsArt
	{
		private static readonly Color32 Front = new Color32(132, 132, 138, 255);
		private static readonly Color32 FrontLow = new Color32(110, 110, 116, 255);
		private static readonly Color32 Top = new Color32(178, 178, 184, 255);
		private static readonly Color32 Side = new Color32(86, 86, 92, 255);
		private static readonly Color32 RecessDark = new Color32(26, 26, 30, 255);
		private static readonly Color32 RecessLight = new Color32(62, 62, 68, 255);
		private static readonly Color32 Plate = new Color32(46, 46, 50, 255);
		private static readonly Color32 PlateTop = new Color32(72, 72, 78, 255);
		private static readonly Color32 Led = new Color32(96, 224, 96, 255);
		private static readonly Color32 Button = new Color32(70, 70, 76, 255);
		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

		/// <summary>Rows of top face and columns of side face that show.</summary>
		private const int TopRows = 3;
		private const int SideColumns = 2;
		/// <summary>Rows of base plate, and how far it reaches past the box on each side.</summary>
		private const int PlateRows = 3;
		private const int PlateReach = 3;

		/// <summary>A casing round a square screen hole.</summary>
		internal sealed class Casing
		{
			public Color32[] Pixels;
			public int Width;
			public int Height;
			/// <summary>The screen hole's bottom left pixel.</summary>
			public int ScreenX;
			public int ScreenY;
		}

		/// <param name="screen">Width of the screen hole in pixels.</param>
		/// <param name="chin">Rows between the box's bottom edge and the screen's recess.</param>
		internal static Casing Draw(int screen, int chin)
		{
			int fw = screen + 8;            // outline, two of plastic, the recess, the screen, and the same on the other side
			int fh = screen + chin + 6;     // outline, chin, recess, screen, recess, two of plastic, outline
			int ox = PlateReach;
			int py = PlateRows - 1;         // the box stands on the plate's top row
			int w = ox + fw + SideColumns + PlateReach;
			int h = py + fh + TopRows;
			// 0 outside, 1 front, 2 top, 3 side, 4 plate.
			byte[] face = new byte[w * h];

			for (int y = 0; y < PlateRows; y++)
			{
				for (int x = 0; x < w; x++)
				{
					face[y * w + x] = 4;
				}
			}
			for (int y = 0; y < fh; y++)
			{
				for (int x = 0; x < fw; x++)
				{
					face[(py + y) * w + ox + x] = 1;
				}
			}
			// The top face: each row up is shifted right, as a box seen from above and to the left.
			for (int j = 0; j < TopRows; j++)
			{
				int shift = (j + 1) * SideColumns / TopRows;
				for (int x = 0; x < fw; x++)
				{
					face[(py + fh + j) * w + ox + x + shift] = 2;
				}
			}
			// The side face: each column right is shifted up the same way.
			for (int c = 0; c < SideColumns; c++)
			{
				int shift = (c + 1) * TopRows / SideColumns;
				for (int y = 0; y < fh; y++)
				{
					int at = (py + y + shift) * w + ox + fw + c;
					if (face[at] == 0)
					{
						face[at] = 3;
					}
				}
			}

			Color32[] px = new Color32[w * h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					int f = face[y * w + x];
					if (f == 0)
					{
						px[y * w + x] = Clear;
						continue;
					}
					if (Edge(face, w, h, x, y))
					{
						px[y * w + x] = PixelMap.Outline;
						continue;
					}
					Color32 c = f == 1 ? (y < py + 1 + chin ? FrontLow : Front) : f == 2 ? Top : f == 3 ? Side : y == PlateRows - 1 ? PlateTop : Plate;
					px[y * w + x] = Speckle(c, x, y, f != 4);
				}
			}

			// The screen's recess: dark along its top and left, lighter along its bottom and right, so it reads as set into the box.
			int sx = ox + 4;
			int sy = py + chin + 2;
			for (int y = sy - 1; y <= sy + screen; y++)
			{
				for (int x = sx - 1; x <= sx + screen; x++)
				{
					bool light = y == sy - 1 || x == sx + screen;
					px[y * w + x] = light ? RecessLight : RecessDark;
				}
			}

			// The chin: a green light on the left, two buttons on the right.
			if (chin >= 2)
			{
				int cy = py + 1 + chin / 2;
				px[cy * w + sx] = Led;
				if (fw >= 18)
				{
					int bx = ox + fw - 5;
					px[cy * w + bx] = Button;
					px[cy * w + bx - 1] = Button;
					px[cy * w + bx - 4] = Button;
					px[cy * w + bx - 5] = Button;
				}
			}
			return new Casing { Pixels = px, Width = w, Height = h, ScreenX = sx, ScreenY = sy };
		}

		/// <summary>A pixel of the shape with a pixel of nothing beside it: drawn as the outline.</summary>
		private static bool Edge(byte[] face, int w, int h, int x, int y)
		{
			return x == 0 || y == 0 || x == w - 1 || y == h - 1 || face[y * w + x - 1] == 0 || face[y * w + x + 1] == 0 || face[(y - 1) * w + x] == 0 || face[(y + 1) * w + x] == 0;
		}

		/// <summary>The rough, speckled surface of the cockpit's pixel art: each pixel a little lighter or darker, and the odd scuff.</summary>
		private static Color32 Speckle(Color32 c, int x, int y, bool scuffs)
		{
			uint hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
			hash ^= hash >> 13;
			hash *= 0x5bd1e995;
			hash ^= hash >> 15;
			int d = (int)(hash % 13) - 6;
			if (scuffs && hash % 29 == 0)
			{
				d -= 22;
			}
			return new Color32((byte)Mathf.Clamp(c.r + d, 0, 255), (byte)Mathf.Clamp(c.g + d, 0, 255), (byte)Mathf.Clamp(c.b + d, 0, 255), 255);
		}

		/// <summary>The shop icon, <paramref name="w"/> by <paramref name="h"/>: the GPS with a lap and the player's arrow on its screen.</summary>
		internal static Color32[] Icon(int w, int h)
		{
			Color32[] px = new Color32[w * h];
			for (int i = 0; i < px.Length; i++)
			{
				px[i] = Clear;
			}
			int chin = h >= 24 ? 3 : 2;
			int screen = Mathf.Min(w - 2 - (8 + SideColumns + 2 * PlateReach), h - 2 - (PlateRows - 1 + chin + 6 + TopRows));
			if (screen < 6)
			{
				return px;
			}
			Casing casing = Draw(screen, chin);
			int ox = (w - casing.Width) / 2;
			int oy = (h - casing.Height) / 2;
			Blit(casing.Pixels, casing.Width, casing.Height, px, w, h, ox, oy);

			// A lap on the screen: grass, a dirt loop, the player's arrow on its bottom stretch.
			Palette p = Palettes.Forest;
			Color32 ground = p.Ground;
			Color32 road = Color.Lerp(p.Road, Color.white, 0.25f);
			Color32 arrow = new Color32(245, 245, 240, 255);
			float half = screen * 0.5f;
			for (int y = 0; y < screen; y++)
			{
				for (int x = 0; x < screen; x++)
				{
					float u = (x + 0.5f - half) / (half * 0.72f);
					float v = (y + 0.5f - half) / (half * 0.56f);
					float r = Mathf.Sqrt(u * u + v * v);
					bool onRoad = Mathf.Abs(r - 1f) * half * 0.64f < 0.75f;
					int at = (oy + casing.ScreenY + y) * w + ox + casing.ScreenX + x;
					px[at] = onRoad ? road : ground;
				}
			}
			int ax = ox + casing.ScreenX + screen / 2;
			int ay = oy + casing.ScreenY + Mathf.RoundToInt(half - half * 0.56f);
			Put(px, w, h, ax, ay + 1, arrow);
			Put(px, w, h, ax - 1, ay, arrow);
			Put(px, w, h, ax, ay, arrow);
			Put(px, w, h, ax + 1, ay, arrow);
			return px;
		}

		private static void Blit(Color32[] from, int fw, int fh, Color32[] to, int tw, int th, int ox, int oy)
		{
			for (int y = 0; y < fh; y++)
			{
				for (int x = 0; x < fw; x++)
				{
					Color32 c = from[y * fw + x];
					if (c.a > 0)
					{
						Put(to, tw, th, ox + x, oy + y, c);
					}
				}
			}
		}

		private static void Put(Color32[] px, int w, int h, int x, int y, Color32 c)
		{
			if (x >= 0 && y >= 0 && x < w && y < h)
			{
				px[y * w + x] = c;
			}
		}
	}
}
