using System;
using System.Text;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The GPS on the dash, made by the game from <see cref="Gps.Prefab"/> when the race's car is fitted. It builds its casing and screen
	/// sprites on the mount it was put on, then redraws the screen's <see cref="PixelMap"/> every frame from the <see cref="Race"/>. When
	/// the GPS settings change (the config file is re-read while the game runs, see <see cref="Mod"/>) it builds itself again, so its size
	/// and place on the dash can be tuned without restarting.
	/// </summary>
	internal sealed class GpsScreen : MonoBehaviour
	{
		/// <summary>The screen before the race has a lap to show: dark, with the casing's light on.</summary>
		private static readonly Color32 NoSignal = new Color32(18, 26, 22, 255);

		private PixelMap map;
		private readonly LapCache lap = new LapCache();
		private Texture2D casingTexture;
		private Sprite casingSprite;
		private Sprite screenSprite;
		private GameObject casingPart;
		private GameObject screenPart;
		private SpriteRenderer casingRenderer;
		private Material casingMaterial;
		private bool lit;
		private string built;
		private bool failed;

		private void Start()
		{
			try
			{
				LogDash();
				texel = MeasureDash();
				Build();
			}
			catch (Exception e)
			{
				failed = true;
				Mod.Log.LogWarning("GPS: could not put itself on the dash: " + e);
			}
		}

		/// <summary>Screen pixels per casing pixel: the screen is finer than its plastic, like a real one, so a whole lap stays legible on it.</summary>
		private const int ScreenDetail = 2;
		/// <summary>When the dash cannot be measured: what the cockpit's pixels measured in game (about 0.05 units).</summary>
		private const float FallbackTexel = 0.05f;

		/// <summary>Size of one cockpit pixel in the GPS's own units.</summary>
		private float texel = FallbackTexel;

		/// <summary>The settings the GPS was last built from.</summary>
		private static string Settings() => Mod.GpsWidth.Value.ToString("0.000") + "|" + Mod.GpsOffsetX.Value.ToString("0.000") + "|" + Mod.GpsOffsetY.Value.ToString("0.000") + "|" + Mod.GpsLitCasing.Value;

		/// <summary>
		/// The cockpit's pixel size: the middle value over every sprite showing on the dash round the GPS. One sprite is not enough: some
		/// are drawn much finer than the rest (the magnet's base measured 13 times too small), and the middle value is the cockpit's own.
		/// </summary>
		private float MeasureDash()
		{
			Transform dash = transform.parent != null ? transform.parent.parent : null;
			float own = Mathf.Abs(transform.lossyScale.x);
			if (dash == null || own < 1e-6f)
			{
				return FallbackTexel;
			}
			System.Collections.Generic.List<float> sizes = new System.Collections.Generic.List<float>();
			foreach (SpriteRenderer r in dash.GetComponentsInChildren<SpriteRenderer>(false))
			{
				if (r.sprite == null || r.transform.IsChildOf(transform) || r.sprite.pixelsPerUnit <= 0f)
				{
					continue;
				}
				float t = Mathf.Abs(r.transform.lossyScale.x) / r.sprite.pixelsPerUnit / own;
				if (t > 0f)
				{
					sizes.Add(t);
				}
			}
			if (sizes.Count < 5)
			{
				Mod.Log.LogWarning("GPS: only " + sizes.Count + " sprites on the dash to measure its pixels by; using " + FallbackTexel);
				return FallbackTexel;
			}
			sizes.Sort();
			float middle = sizes[sizes.Count / 2];
			Mod.Log.LogInfo("GPS: dash pixel " + middle.ToString("0.0000") + " units, the middle of " + sizes.Count + " sprites (smallest " + sizes[0].ToString("0.0000") + ", largest " + sizes[sizes.Count - 1].ToString("0.0000") + ")");
			if (middle < 0.005f || middle > 0.5f)
			{
				Mod.Log.LogWarning("GPS: that dash pixel is out of reason; using " + FallbackTexel);
				return FallbackTexel;
			}
			return middle;
		}

		private void Build()
		{
			Clear();
			built = Settings();
			int width = Mathf.Clamp(Mathf.RoundToInt(Mod.GpsWidth.Value / texel), 24, 160);
			int hole = width - GpsArt.Dash.ExtraWidth;
			int screen = hole * ScreenDetail;
			GpsArt.Casing casing = GpsArt.Draw(hole, hole, GpsArt.Dash);
			int layer = transform.parent != null ? transform.parent.gameObject.layer : gameObject.layer;
			gameObject.layer = layer;
			// The screen glows, so it is unlit. The casing is lit like the rest of the cockpit: the dash's sprites each have their own copy
			// of the lit material with their own texture in it (the shader reads the material's texture, not the sprite's), so the casing
			// gets a copy too. LitCasing off falls back to the unlit material, dimmed at night in Update.
			Material material = Gps.Unlit != null ? Gps.Unlit : Gps.Lit;

			casingTexture = Texture(casing.Width, casing.Height, "GpsCasing");
			casingTexture.SetPixels32(casing.Pixels);
			casingTexture.Apply(false, true);
			casingSprite = Sprite.Create(casingTexture, new Rect(0, 0, casing.Width, casing.Height), new Vector2(0.5f, 0f), 1f / texel, 0, SpriteMeshType.FullRect);
			Vector3 foot = Gps.Foot + new Vector3(Mod.GpsOffsetX.Value, Mod.GpsOffsetY.Value, 0f);
			lit = Mod.GpsLitCasing.Value && Gps.Lit != null;
			if (lit)
			{
				casingMaterial = new Material(Gps.Lit);
				casingMaterial.name = "GpsCasingLit";
				casingMaterial.hideFlags = HideFlags.HideAndDontSave;
				foreach (string name in new[] { "_BaseMap", "_MainTex" })
				{
					if (casingMaterial.HasProperty(name))
					{
						casingMaterial.SetTexture(name, casingTexture);
						casingMaterial.SetTextureScale(name, Vector2.one);
						casingMaterial.SetTextureOffset(name, Vector2.zero);
					}
				}
				foreach (string name in new[] { "_BaseColor", "_Color" })
				{
					if (casingMaterial.HasProperty(name))
					{
						casingMaterial.SetColor(name, Color.white);
					}
				}
			}
			casingRenderer = Part("Casing", casingSprite, lit ? casingMaterial : material, foot, 0, layer);
			casingPart = casingRenderer.gameObject;

			map = new PixelMap(screen, false);
			map.Blank(NoSignal);
			map.Upload();
			screenSprite = Sprite.Create(map.Texture, new Rect(0, 0, screen, screen), new Vector2(0.5f, 0.5f), ScreenDetail / texel, 0, SpriteMeshType.FullRect);
			Vector3 middle = foot + new Vector3((casing.ScreenX + hole * 0.5f - casing.Width * 0.5f) * texel, (casing.ScreenY + hole * 0.5f) * texel, 0f);
			screenPart = Part("Screen", screenSprite, material, middle, 1, layer).gameObject;
			Mod.Log.LogInfo("GPS: on " + (transform.parent != null ? transform.parent.name : "?") + ", " + casing.Width + "x" + casing.Height + " px casing, " + screen + " px screen, " + (casing.Width * texel).ToString("0.00") + " units wide, foot " + foot.ToString("0.00") + ", casing material '" + (lit ? Gps.Lit.name + "' (" + Gps.Lit.shader.name + ", own copy)" : (material != null ? material.name : "default") + "'"));
		}

		/// <summary>
		/// Writes the dash round the GPS to the log: every mount slot and the sprites showing in it, with positions in the GPS's own units.
		/// For placing the GPS by hand: the module slots, the slow-mo buttons and the nitro lever all show up here.
		/// </summary>
		private void LogDash()
		{
			Transform mount = transform.parent;
			Transform dash = mount != null ? mount.parent : null;
			if (dash == null)
			{
				return;
			}
			StringBuilder text = new StringBuilder("GPS: the dash round it (positions in the GPS's units):");
			foreach (Transform slot in dash)
			{
				if (slot.name.IndexOf("SLOT", StringComparison.OrdinalIgnoreCase) < 0)
				{
					continue;
				}
				text.Append("\n  ").Append(slot.name).Append(" at ").Append(transform.InverseTransformPoint(slot.position).ToString("0.00"));
				foreach (SpriteRenderer r in slot.GetComponentsInChildren<SpriteRenderer>(false))
				{
					if (r.transform.IsChildOf(transform) || r.sprite == null)
					{
						continue;
					}
					Bounds b = r.bounds;
					text.Append("\n    ").Append(r.name).Append(": centre ").Append(transform.InverseTransformPoint(b.center).ToString("0.00"))
						.Append(", size ").Append((b.size.x / Mathf.Max(1e-5f, transform.lossyScale.x)).ToString("0.00")).Append(" x ").Append((b.size.y / Mathf.Max(1e-5f, transform.lossyScale.y)).ToString("0.00"))
						.Append(", order ").Append(r.sortingOrder).Append(", material '").Append(r.sharedMaterial != null ? r.sharedMaterial.name : "none").Append("'");
				}
			}
			Mod.Log.LogInfo(text.ToString());
		}

		private SpriteRenderer Part(string name, Sprite sprite, Material material, Vector3 position, int above, int layer)
		{
			GameObject part = new GameObject(name);
			part.layer = layer;
			part.transform.SetParent(transform, false);
			part.transform.localPosition = position;
			SpriteRenderer r = part.AddComponent<SpriteRenderer>();
			r.sprite = sprite;
			if (material != null)
			{
				r.sharedMaterial = material;
			}
			r.sortingLayerID = Gps.SortingLayer;
			r.sortingOrder = Gps.SortingOrder + above;
			return r;
		}

		private static Texture2D Texture(int w, int h, string name)
		{
			Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
			tex.name = name;
			tex.filterMode = FilterMode.Point;
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.hideFlags = HideFlags.HideAndDontSave;
			return tex;
		}

		private void Update()
		{
			if (failed)
			{
				return;
			}
			try
			{
				if (built != Settings())
				{
					Build();
				}
				WeatherProfile weather = WeatherGrip.Current ?? RunState.ActiveWeather;
				float shade = !lit && weather != null && weather.headlightsActive ? 0.6f : 1f;
				casingRenderer.color = new Color(shade, shade, shade, 1f);

				Race.Tick();
				if (Race.Ready)
				{
					map.Draw(lap.Get(map.Size, map.Room), Race.Palette, Race.Heading, Race.Player, Race.Cars);
				}
				else
				{
					map.Blank(NoSignal);
				}
				map.Upload();
			}
			catch (Exception e)
			{
				failed = true;
				Mod.Log.LogWarning("GPS: stopped after an error: " + e);
			}
		}

		/// <summary>Takes down the parts, textures and sprites of the last build.</summary>
		private void Clear()
		{
			if (casingPart != null)
			{
				Destroy(casingPart);
			}
			if (screenPart != null)
			{
				Destroy(screenPart);
			}
			map?.Destroy();
			map = null;
			if (casingTexture != null)
			{
				Destroy(casingTexture);
			}
			if (casingSprite != null)
			{
				Destroy(casingSprite);
			}
			if (screenSprite != null)
			{
				Destroy(screenSprite);
			}
			if (casingMaterial != null)
			{
				Destroy(casingMaterial);
			}
			casingMaterial = null;
			casingPart = screenPart = null;
			casingTexture = null;
			casingSprite = screenSprite = null;
		}

		private void OnDestroy() => Clear();
	}
}
