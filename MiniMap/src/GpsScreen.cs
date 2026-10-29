using System;
using UnityEngine;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The GPS on the dash, made by the game from <see cref="Gps.Prefab"/> when the race's car is fitted. It builds its casing and screen
	/// sprites on the mount it was put on, then redraws the screen's <see cref="PixelMap"/> every frame from the <see cref="Race"/>.
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
		private bool failed;

		private void Start()
		{
			try
			{
				Build();
			}
			catch (Exception e)
			{
				failed = true;
				Mod.Log.LogWarning("GPS: could not put itself on the dash: " + e);
			}
		}

		private void Build()
		{
			float texel = Gps.Texel;
			int width = Mathf.Clamp(Mathf.RoundToInt(Mod.GpsWidth.Value / texel), 24, 200);
			int screen = width - 6;
			GpsArt.Casing casing = GpsArt.Draw(screen, 3, 5);
			int layer = transform.parent != null ? transform.parent.gameObject.layer : gameObject.layer;
			gameObject.layer = layer;
			float ppu = 1f / texel;

			casingTexture = Texture(casing.Width, casing.Height, "GpsCasing");
			casingTexture.SetPixels32(casing.Pixels);
			casingTexture.Apply(false, true);
			casingSprite = Sprite.Create(casingTexture, new Rect(0, 0, casing.Width, casing.Height), new Vector2(0.5f, 0f), ppu);
			Vector3 foot = Gps.Foot + new Vector3(Mod.GpsOffsetX.Value, Mod.GpsOffsetY.Value, 0f);
			Part("Casing", casingSprite, Gps.Lit, foot, 0, layer);

			map = new PixelMap(screen, false);
			map.Blank(NoSignal);
			map.Upload();
			screenSprite = Sprite.Create(map.Texture, new Rect(0, 0, screen, screen), new Vector2(0.5f, 0.5f), ppu);
			Vector3 middle = foot + new Vector3((casing.ScreenX + screen * 0.5f - casing.Width * 0.5f) * texel, (casing.ScreenY + screen * 0.5f) * texel, 0f);
			Part("Screen", screenSprite, Gps.Unlit != null ? Gps.Unlit : Gps.Lit, middle, 1, layer);
			Mod.Info("GPS: on " + (transform.parent != null ? transform.parent.name : "?") + ", " + casing.Width + "x" + casing.Height + " px casing, " + screen + " px screen, " + (casing.Width * texel).ToString("0.00") + " units wide");
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
			if (failed || map == null)
			{
				return;
			}
			try
			{
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

		private void OnDestroy()
		{
			map?.Destroy();
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
		}
	}
}
