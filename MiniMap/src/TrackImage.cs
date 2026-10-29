using System.Collections.Generic;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The minimap's picture: the lap drawn from above onto a square texture, ground colour all round, the road with a line along each
	/// edge, and a chequered start line. The lap is fitted inside the circle the square holds, so however the map is turned none of it is
	/// cut off by the round frame.
	/// </summary>
	internal sealed class TrackImage
	{
		/// <summary>Side of the texture in pixels.</summary>
		internal const int Side = 512;
		/// <summary>Half the drawn road, in texture pixels. A fixed width, not the road's: the track line is the bots' line, and on a whole lap a true width would be a hair.</summary>
		private const float RoadHalf = 5.5f;
		/// <summary>The edge line drawn outside the road, in texture pixels.</summary>
		private const float EdgeWidth = 2f;
		/// <summary>Kept clear at the rim, in texture pixels: the frame ring (9 of 256, so 18 here) lies over it, and car markers need room.</summary>
		private const float Rim = 28f;

		internal Texture2D Texture;
		/// <summary>The world point (x, z) at the middle of the texture.</summary>
		internal Vector2 Centre;
		/// <summary>Texture pixels per metre.</summary>
		internal float Scale;

		/// <summary>Where a world position is on the texture, in pixels from its middle.</summary>
		internal Vector2 ToPixels(Vector3 world) => new Vector2(world.x - Centre.x, world.z - Centre.y) * Scale;

		internal static TrackImage Build(List<Vector3> points, Palette palette)
		{
			int n = points.Count;
			Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
			Vector2 max = new Vector2(float.MinValue, float.MinValue);
			foreach (Vector3 p in points)
			{
				min = Vector2.Min(min, new Vector2(p.x, p.z));
				max = Vector2.Max(max, new Vector2(p.x, p.z));
			}
			Vector2 centre = (min + max) * 0.5f;
			float radius = 1f;
			foreach (Vector3 p in points)
			{
				radius = Mathf.Max(radius, (new Vector2(p.x, p.z) - centre).magnitude);
			}

			TrackImage image = new TrackImage { Centre = centre };
			image.Scale = (Side * 0.5f - RoadHalf - EdgeWidth - Rim) / radius;

			Vector2[] px = new Vector2[n];
			for (int i = 0; i < n; i++)
			{
				px[i] = image.ToPixels(points[i]) + new Vector2(Side * 0.5f, Side * 0.5f);
			}

			// How much of each pixel the road and its edge cover, from the distance to the nearest stretch of line, smoothed over a pixel.
			float[] road = new float[Side * Side];
			float[] edge = new float[Side * Side];
			float reach = RoadHalf + EdgeWidth;
			for (int i = 0; i < n; i++)
			{
				Vector2 a = px[i];
				Vector2 b = px[(i + 1) % n];
				int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach - 1));
				int x1 = Mathf.Min(Side - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach + 1));
				int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach - 1));
				int y1 = Mathf.Min(Side - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach + 1));
				for (int y = y0; y <= y1; y++)
				{
					for (int x = x0; x <= x1; x++)
					{
						float d = Distance(new Vector2(x + 0.5f, y + 0.5f), a, b);
						int at = y * Side + x;
						edge[at] = Mathf.Max(edge[at], Mathf.Clamp01(reach + 0.5f - d));
						road[at] = Mathf.Max(road[at], Mathf.Clamp01(RoadHalf + 0.5f - d));
					}
				}
			}

			Color32[] pixels = new Color32[Side * Side];
			for (int i = 0; i < pixels.Length; i++)
			{
				Color c = Color.Lerp(palette.Ground, palette.Edge, edge[i]);
				pixels[i] = Color.Lerp(c, palette.Road, road[i]);
			}
			StartLine(pixels, px);

			Texture2D tex = new Texture2D(Side, Side, TextureFormat.RGBA32, false);
			tex.name = "MiniMapTrack";
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Bilinear;
			tex.hideFlags = HideFlags.HideAndDontSave;
			tex.SetPixels32(pixels);
			tex.Apply(false, true);
			image.Texture = tex;
			return image;
		}

		/// <summary>A black and white chequered bar across the road at the first point of the line, where the lap counter starts a lap.</summary>
		private static void StartLine(Color32[] pixels, Vector2[] px)
		{
			int n = px.Length;
			Vector2 o = px[0];
			Vector2 along = (px[1] - px[n - 1]).normalized;
			Vector2 across = new Vector2(-along.y, along.x);
			float half = RoadHalf + EdgeWidth;
			const float thick = 2f;
			const float square = 2f;
			int r = Mathf.CeilToInt(half + thick + 1);
			for (int y = (int)o.y - r; y <= (int)o.y + r; y++)
			{
				for (int x = (int)o.x - r; x <= (int)o.x + r; x++)
				{
					if (x < 0 || y < 0 || x >= Side || y >= Side)
					{
						continue;
					}
					Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - o;
					float u = Vector2.Dot(d, across);
					float v = Vector2.Dot(d, along);
					if (Mathf.Abs(u) > half || Mathf.Abs(v) > thick)
					{
						continue;
					}
					bool white = (Mathf.FloorToInt((u + half) / square) + Mathf.FloorToInt((v + thick) / square)) % 2 == 0;
					pixels[y * Side + x] = white ? new Color32(245, 245, 245, 255) : new Color32(20, 20, 20, 255);
				}
			}
		}

		private static float Distance(Vector2 p, Vector2 a, Vector2 b)
		{
			Vector2 ab = b - a;
			float len = ab.sqrMagnitude;
			float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
			return (p - (a + ab * t)).magnitude;
		}
	}
}
