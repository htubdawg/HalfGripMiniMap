using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>The minimap's sprites, drawn in code (a disc for the mask, the ring round the map, the car markers) so the mod ships no art.</summary>
	internal static class Shapes
	{
		/// <summary>A filled disc, smooth at the rim, with an optional outline in another colour.</summary>
		internal static Sprite Disc(int size, Color fill, Color outline, float outlineWidth)
		{
			float r = size * 0.5f;
			return Make(size, (x, y) =>
			{
				float d = new Vector2(x - r, y - r).magnitude - (r - 1f);
				return Paint(d, fill, outline, outlineWidth);
			});
		}

		/// <summary>A ring of this thickness at the edge of the square.</summary>
		internal static Sprite Ring(int size, float thickness, Color colour)
		{
			float r = size * 0.5f;
			return Make(size, (x, y) =>
			{
				float d = new Vector2(x - r, y - r).magnitude;
				float outer = Mathf.Clamp01(r - 1f - d + 0.5f);
				float inner = Mathf.Clamp01(d - (r - 1f - thickness) + 0.5f);
				Color c = colour;
				c.a *= Mathf.Min(outer, inner);
				return c;
			});
		}

		/// <summary>The player's marker: an arrowhead pointing up, the way the car points, with a dark outline.</summary>
		internal static Sprite Arrow(int size, Color fill, Color outline, float outlineWidth)
		{
			float s = size;
			// Corners, clockwise from the tip: a triangle with a notch cut into its base.
			Vector2[] corners =
			{
				new Vector2(0.5f * s, 0.94f * s),
				new Vector2(0.86f * s, 0.08f * s),
				new Vector2(0.5f * s, 0.28f * s),
				new Vector2(0.14f * s, 0.08f * s),
			};
			return Make(size, (x, y) => Paint(Polygon(new Vector2(x, y), corners), fill, outline, outlineWidth));
		}

		/// <summary>Colour for a pixel this far outside a shape's edge (negative is inside).</summary>
		private static Color Paint(float d, Color fill, Color outline, float outlineWidth)
		{
			float shape = Mathf.Clamp01(0.5f - d);
			float body = Mathf.Clamp01(0.5f - d - outlineWidth);
			Color c = Color.Lerp(outline, fill, body);
			c.a *= shape;
			return c;
		}

		/// <summary>Distance to a closed polygon's outline, negative inside (even-odd rule, so a notched outline works).</summary>
		private static float Polygon(Vector2 p, Vector2[] v)
		{
			float best = float.MaxValue;
			bool inside = false;
			for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
			{
				Vector2 a = v[j];
				Vector2 b = v[i];
				Vector2 ab = b - a;
				float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
				best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
				if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x))
				{
					inside = !inside;
				}
			}
			return inside ? -best : best;
		}

		private static Sprite Make(int size, System.Func<float, float, Color> pixel)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Bilinear;
			tex.hideFlags = HideFlags.HideAndDontSave;
			Color[] pixels = new Color[size * size];
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					pixels[y * size + x] = pixel(x + 0.5f, y + 0.5f);
				}
			}
			tex.SetPixels(pixels);
			tex.Apply(false, true);
			Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
			sprite.hideFlags = HideFlags.HideAndDontSave;
			return sprite;
		}
	}
}
