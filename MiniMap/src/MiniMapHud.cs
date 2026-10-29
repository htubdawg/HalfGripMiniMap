using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfGripMiniMap
{
	/// <summary>
	/// Shows the minimap, and lives for the whole game. In each scene it looks for the race's <see cref="TrackManager"/>, draws its track
	/// line, and then each frame redraws the <see cref="PixelMap"/> turned to the player's heading with every car on it. A map mod may
	/// build or replace the track line some time after the scene loads (ProcTrack does, in its own scene callback), so the line is
	/// checked twice a second and the lap is drawn again whenever it has changed.
	/// </summary>
	internal sealed class MiniMapHud : MonoBehaviour
	{
		private RectTransform root;
		private RawImage view;

		private string scene = "";
		private TrackManager tm;
		private float nextSearch;
		private float nextCheck;
		private string drawn;
		private TrackImage image;
		private Palette palette;
		private PixelMap pixels;
		private bool failed;
		private readonly List<KeyValuePair<Vector3, Marker>> cars = new List<KeyValuePair<Vector3, Marker>>();

		internal void OnSceneLoaded(Scene loaded)
		{
			scene = loaded.name;
			tm = null;
			drawn = null;
			image = null;
			nextSearch = 0f;
			failed = false;
		}

		private void Awake()
		{
			try
			{
				BuildUi();
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("Minimap: could not make its HUD, it stays off: " + e);
				enabled = false;
			}
		}

		private void OnDestroy()
		{
			if (pixels != null)
			{
				Destroy(pixels.Texture);
			}
		}

		private void Update()
		{
			if (failed)
			{
				return;
			}
			try
			{
				if (Mod.Toggle != null && Mod.Toggle.Down)
				{
					Mod.Shown.Value = !Mod.Shown.Value;
				}
				bool ready = Track();
				bool show = ready && Mod.Shown.Value && !InteriorMath.Paused && tm.playerCarController != null;
				if (root.gameObject.activeSelf != show)
				{
					root.gameObject.SetActive(show);
				}
				if (show)
				{
					Place();
					Draw();
				}
			}
			catch (Exception e)
			{
				// One warning per scene, not one per frame.
				failed = true;
				root.gameObject.SetActive(false);
				Mod.Log.LogWarning("Minimap: stopped for this scene after an error: " + e);
			}
		}

		/// <summary>The minimap's width in its own pixels: the set size cut into pixels of the set pixel size, kept even so it has a middle.</summary>
		private static int Resolution() => Mathf.Clamp(Mathf.RoundToInt(Mod.Size.Value / (float)Mod.PixelSize.Value / 2f) * 2, 40, 300);

		/// <summary>Finds the race and keeps the lap's picture in step with its track line. False while there is nothing to show.</summary>
		private bool Track()
		{
			if (tm == null)
			{
				if (Time.unscaledTime < nextSearch)
				{
					return false;
				}
				nextSearch = Time.unscaledTime + 1f;
				tm = FindFirstObjectByType<TrackManager>();
				if (tm == null)
				{
					return false;
				}
				Mod.Info("Minimap: race found in scene " + scene);
			}
			PathManager line = tm.lapManager != null ? tm.lapManager.trackLine : null;
			if (line == null || line.sampledPoints == null || line.sampledPoints.Count < 8)
			{
				return false;
			}
			int size = Resolution();
			if (pixels == null || pixels.Size != size)
			{
				if (pixels != null)
				{
					Destroy(pixels.Texture);
				}
				pixels = new PixelMap(size);
				view.texture = pixels.Texture;
				drawn = null;
			}
			if (image == null || drawn == null || Time.unscaledTime >= nextCheck)
			{
				nextCheck = Time.unscaledTime + 0.5f;
				WeatherProfile weather = WeatherGrip.Current ?? RunState.ActiveWeather;
				string now = Signature(line, weather, size);
				if (now != drawn)
				{
					Redraw(line, weather, size);
					drawn = now;
				}
			}
			return image != null;
		}

		/// <summary>What the lap's picture was drawn from: the line's points and length, the weather that coloured it, and the minimap's size.</summary>
		private static string Signature(PathManager line, WeatherProfile weather, int size)
		{
			List<Vector3> pts = line.sampledPoints;
			return pts.Count + "|" + pts[0] + "|" + pts[pts.Count / 3] + "|" + pts[2 * pts.Count / 3] + "|" + line.trackLength.ToString("0.0") + "|" + (weather != null ? weather.GetInstanceID() : 0) + "|" + size;
		}

		private void Redraw(PathManager line, WeatherProfile weather, int size)
		{
			palette = Palettes.For(scene, weather, line);
			image = TrackImage.Build(line.sampledPoints, palette, size, PixelMap.Ring);
			Mod.Info("Minimap: drew a " + line.trackLength.ToString("0") + " m lap of " + line.sampledPoints.Count + " points in " + scene + ", colours " + palette.Name + (weather != null ? " (weather " + weather.name + ")" : "") + ", " + size + " px wide, " + (image.Scale / TrackImage.Detail).ToString("0.000") + " px/m");
		}

		/// <summary>Puts the minimap in its corner, a whole number of screen pixels per map pixel (at 1080p); the settings can change while the game runs.</summary>
		private void Place()
		{
			float size = pixels.Size * Mod.PixelSize.Value;
			float margin = Mod.Margin.Value;
			string corner = Mod.Corner.Value ?? "";
			float x = corner.EndsWith("Right") ? 1f : 0f;
			float y = corner.StartsWith("Bottom") ? 0f : 1f;
			Vector2 anchor = new Vector2(x, y);
			root.anchorMin = anchor;
			root.anchorMax = anchor;
			root.pivot = anchor;
			root.anchoredPosition = new Vector2(x > 0.5f ? -margin : margin, y > 0.5f ? -margin : margin);
			root.sizeDelta = new Vector2(size, size);
			view.color = new Color(1f, 1f, 1f, Mod.Opacity.Value);
		}

		/// <summary>Redraws the minimap for this frame: turned to the player's heading, every other car on it.</summary>
		private void Draw()
		{
			Transform car = tm.playerCarController.transform;
			Vector3 forward = car.forward;
			float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

			cars.Clear();
			Dictionary<GameObject, LapManager.LeaderboardEntry> board = tm.lapManager.leaderboard;
			if (board != null && board.Count > 0)
			{
				GameObject self = tm.lapManager.PlayerCar;
				foreach (GameObject other in board.Keys)
				{
					if (other != null && other != self && other.activeInHierarchy && other.transform != car && !car.IsChildOf(other.transform))
					{
						cars.Add(new KeyValuePair<Vector3, Marker>(other.transform.position, other.GetComponent("RemoteCar") != null ? Marker.Remote : Marker.Bot));
					}
				}
			}
			else if (tm.Enemies != null)
			{
				foreach (EnemyController enemy in tm.Enemies)
				{
					if (enemy != null && enemy.gameObject.activeInHierarchy)
					{
						cars.Add(new KeyValuePair<Vector3, Marker>(enemy.transform.position, Marker.Bot));
					}
				}
			}
			pixels.Draw(image, palette, heading, car.position, cars);
		}

		private void BuildUi()
		{
			GameObject canvasObject = new GameObject("MiniMapCanvas");
			canvasObject.transform.SetParent(transform, false);
			Canvas canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 50;
			canvas.pixelPerfect = true;
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 1f;

			view = new GameObject("MiniMap", typeof(RectTransform)).AddComponent<RawImage>();
			view.raycastTarget = false;
			root = view.rectTransform;
			root.SetParent(canvasObject.transform, false);
			root.gameObject.SetActive(false);
		}
	}
}
