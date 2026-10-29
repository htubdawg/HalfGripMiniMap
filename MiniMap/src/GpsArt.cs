using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The GPS's pixel art, drawn in code so it fits whatever size the dash and the shop card need: the casing that stands on the dash
	/// (a dark plastic box on a short stand, a recessed square screen, a green light and two buttons on the chin) and the shop icon (the
	/// same box with a lap on its screen). Pixel arrays only, rows from the bottom; the caller makes the textures.
	/// </summary>
	internal static class GpsArt
	{
		private static readonly Color32 Body = new Color32(58, 58, 64, 255);
		private static readonly Color32 Light = new Color32(88, 88, 96, 255);
		private static readonly Color32 Shade = new Color32(40, 40, 45, 255);
		private static readonly Color32 Recess = new Color32(22, 22, 26, 255);
		private static readonly Color32 Stand = new Color32(32, 32, 36, 255);
		private static readonly Color32 Led = new Color32(96, 224, 96, 255);
		private static readonly Color32 Button = new Color32(112, 112, 120, 255);
		private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

		/// <summary>A casing round a square screen <paramref name="screen"/> pixels wide.</summary>
		internal sealed class Casing
		{
			public Color32[] Pixels;
			public int Width;
			public int Height;
			/// <summary>The screen's bottom left pixel.</summary>
			public int ScreenX;
			public int ScreenY;
		}

		/// <param name="screen">Width of the screen in pixels.</param>
		/// <param name="stand">Rows of stand under the box.</param>
		/// <param name="chin">Rows of chin between the box's bottom edge and the screen's recess.</param>
		internal static Casing Draw(int screen, int stand, int chin)
		{
			// Each side of the screen: its recess, a pixel of body and the outline.
			int w = screen + 6;
			int screenY = stand + 1 + chin + 1;
			int h = screenY + screen + 3;
			Color32[] px = new Color32[w * h];
			for (int i = 0; i < px.Length; i++)
			{
				px[i] = Clear;
			}

			// The stand: a short dark neck in the middle, under the box.
			int sw = Mathf.Max(4, w / 3);
			int sx = (w - sw) / 2;
			for (int y = 0; y < stand; y++)
			{
				for (int x = sx; x < sx + sw; x++)
				{
					px[y * w + x] = x == sx || x == sx + sw - 1 ? PixelMap.Outline : Stand;
				}
			}

			// The box, its corners cut for a rounded look, lit along the top and shaded along the bottom.
			int bottom = stand;
			int top = h - 1;
			for (int y = bottom; y <= top; y++)
			{
				for (int x = 0; x < w; x++)
				{
					bool edgeX = x == 0 || x == w - 1;
					bool edgeY = y == bottom || y == top;
					if (edgeX && edgeY)
					{
						continue;
					}
					Color32 c = edgeX || edgeY ? PixelMap.Outline : y == top - 1 ? Light : y == bottom + 1 ? Shade : Body;
					px[y * w + x] = c;
				}
			}

			// The recess the screen sits in: one dark pixel all round it.
			for (int y = screenY - 1; y <= screenY + screen; y++)
			{
				for (int x = 2; x <= w - 3; x++)
				{
					px[y * w + x] = Recess;
				}
			}

			// The chin: a green light on the left, two buttons on the right.
			if (chin >= 2)
			{
				int cy = bottom + 1 + chin / 2;
				px[cy * w + 3] = Led;
				if (w >= 16)
				{
					px[cy * w + (w - 5)] = Button;
					px[cy * w + (w - 8)] = Button;
				}
			}
			return new Casing { Pixels = px, Width = w, Height = h, ScreenX = 3, ScreenY = screenY };
		}

		/// <summary>The shop icon, <paramref name="w"/> by <paramref name="h"/>: the GPS with a lap and the player's arrow on its screen.</summary>
		internal static Color32[] Icon(int w, int h)
		{
			Color32[] px = new Color32[w * h];
			for (int i = 0; i < px.Length; i++)
			{
				px[i] = Clear;
			}
			int stand = h >= 24 ? 2 : 1;
			int chin = h >= 24 ? 3 : 2;
			int screen = Mathf.Min(w - 8, h - 2 - (stand + chin + 5));
			if (screen < 6)
			{
				return px;
			}
			Casing casing = Draw(screen, stand, chin);
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
