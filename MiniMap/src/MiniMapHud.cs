using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfGripMiniMap
{
	/// <summary>
	/// Draws the minimap, and lives for the whole game. In each scene it looks for the race's <see cref="TrackManager"/>, draws its track
	/// line, and then each frame turns the map so the player's car points up and moves the car markers. A map mod may build or replace the
	/// track line some time after the scene loads (ProcTrack does, in its own scene callback), so the line is checked twice a second and
	/// the picture is drawn again whenever it has changed.
	/// </summary>
	internal sealed class MiniMapHud : MonoBehaviour
	{
		private static readonly Color PlayerColour = Color.white;
		private static readonly Color BotColour = Palettes.Rgb(0xe8433a);
		/// <summary>A car of the Multiplayer mod's (another player in the room).</summary>
		private static readonly Color RemoteColour = Palettes.Rgb(0x3fa9f5);
		private static readonly Color Outline = new Color(0.05f, 0.05f, 0.06f, 1f);

		private RectTransform root;
		private CanvasGroup group;
		private RectTransform map;
		private RawImage mapImage;
		private Image frame;
		private Image player;
		private readonly List<Image> others = new List<Image>();
		private Sprite botDot;
		private Sprite remoteDot;

		private string scene = "";
		private TrackManager tm;
		private float nextSearch;
		private float nextCheck;
		private string drawn;
		private TrackImage image;
		private bool failed;

		internal void OnSceneLoaded(Scene loaded)
		{
			scene = loaded.name;
			tm = null;
			drawn = null;
			nextSearch = 0f;
			failed = false;
			Drop();
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

		private void OnDestroy() => Drop();

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

		/// <summary>Finds the race and keeps the picture in step with its track line. False while there is nothing to show.</summary>
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
			if (image == null || Time.unscaledTime >= nextCheck)
			{
				nextCheck = Time.unscaledTime + 0.5f;
				WeatherProfile weather = WeatherGrip.Current ?? RunState.ActiveWeather;
				string now = Signature(line, weather);
				if (now != drawn)
				{
					Redraw(line, weather);
					drawn = now;
				}
			}
			return image != null;
		}

		/// <summary>What the picture was drawn from: the line's points and length, and the weather that coloured it.</summary>
		private static string Signature(PathManager line, WeatherProfile weather)
		{
			List<Vector3> pts = line.sampledPoints;
			return pts.Count + "|" + pts[0] + "|" + pts[pts.Count / 3] + "|" + pts[2 * pts.Count / 3] + "|" + line.trackLength.ToString("0.0") + "|" + (weather != null ? weather.GetInstanceID() : 0);
		}

		private void Redraw(PathManager line, WeatherProfile weather)
		{
			Palette palette = Palettes.For(scene, weather, line);
			TrackImage made = TrackImage.Build(line.sampledPoints, palette);
			Drop();
			image = made;
			mapImage.texture = image.Texture;
			frame.color = palette.Frame;
			Mod.Info("Minimap: drew a " + line.trackLength.ToString("0") + " m lap of " + line.sampledPoints.Count + " points in " + scene + ", colours " + palette.Name + (weather != null ? " (weather " + weather.name + ")" : "") + ", " + image.Scale.ToString("0.00") + " px/m");
		}

		/// <summary>Lets go of the current picture.</summary>
		private void Drop()
		{
			if (image != null && image.Texture != null)
			{
				Destroy(image.Texture);
			}
			image = null;
			if (mapImage != null)
			{
				mapImage.texture = null;
			}
		}

		/// <summary>Puts the map in its corner at its size; the settings can change while the game runs.</summary>
		private void Place()
		{
			float size = Mod.Size.Value;
			float margin = Mod.Margin.Value;
			string corner = Mod.Corner.Value ?? "";
			float x = corner.EndsWith("Left") ? 0f : 1f;
			float y = corner.StartsWith("Bottom") ? 0f : 1f;
			Vector2 anchor = new Vector2(x, y);
			root.anchorMin = anchor;
			root.anchorMax = anchor;
			root.pivot = anchor;
			root.anchoredPosition = new Vector2(x > 0.5f ? -margin : margin, y > 0.5f ? -margin : margin);
			root.sizeDelta = new Vector2(size, size);
			// The map is as wide as the round window: a square turned any way still covers the circle inside it.
			map.sizeDelta = new Vector2(size, size);
			group.alpha = Mod.Opacity.Value;
		}

		/// <summary>Turns the map to the player's heading and moves every car's marker.</summary>
		private void Draw()
		{
			float size = Mod.Size.Value;
			float perPixel = size / TrackImage.Side;
			Transform car = tm.playerCarController.transform;
			Vector3 forward = car.forward;
			float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
			// The map turns one way and every marker the other, so markers stay upright on screen and the player's arrow points up.
			map.localEulerAngles = new Vector3(0f, 0f, heading);
			Quaternion upright = Quaternion.Euler(0f, 0f, -heading);

			int used = 0;
			Dictionary<GameObject, LapManager.LeaderboardEntry> board = tm.lapManager.leaderboard;
			if (board != null && board.Count > 0)
			{
				GameObject self = tm.lapManager.PlayerCar;
				foreach (GameObject other in board.Keys)
				{
					if (other != null && other != self && other.activeInHierarchy && other.transform != car && !car.IsChildOf(other.transform))
					{
						Mark(used++, other, perPixel, size, upright);
					}
				}
			}
			else if (tm.Enemies != null)
			{
				foreach (EnemyController enemy in tm.Enemies)
				{
					if (enemy != null && enemy.gameObject.activeInHierarchy)
					{
						Mark(used++, enemy.gameObject, perPixel, size, upright);
					}
				}
			}
			for (int i = used; i < others.Count; i++)
			{
				if (others[i].gameObject.activeSelf)
				{
					others[i].gameObject.SetActive(false);
				}
			}

			float playerSize = size * 0.075f;
			RectTransform p = player.rectTransform;
			p.sizeDelta = new Vector2(playerSize, playerSize);
			p.anchoredPosition = image.ToPixels(car.position) * perPixel;
			p.localRotation = upright;
			p.SetAsLastSibling();
		}

		private void Mark(int index, GameObject car, float perPixel, float size, Quaternion upright)
		{
			while (others.Count <= index)
			{
				others.Add(MakeImage("Car", map, botDot));
			}
			Image dot = others[index];
			if (!dot.gameObject.activeSelf)
			{
				dot.gameObject.SetActive(true);
			}
			dot.sprite = car.GetComponent("RemoteCar") != null ? remoteDot : botDot;
			float dotSize = size * 0.05f;
			RectTransform r = dot.rectTransform;
			r.sizeDelta = new Vector2(dotSize, dotSize);
			r.anchoredPosition = image.ToPixels(car.transform.position) * perPixel;
			r.localRotation = upright;
		}

		private void BuildUi()
		{
			GameObject canvasObject = new GameObject("MiniMapCanvas");
			canvasObject.transform.SetParent(transform, false);
			Canvas canvas = canvasObject.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 50;
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920f, 1080f);
			scaler.matchWidthOrHeight = 1f;

			root = new GameObject("MiniMap", typeof(RectTransform)).GetComponent<RectTransform>();
			root.SetParent(canvasObject.transform, false);
			group = root.gameObject.AddComponent<CanvasGroup>();
			group.blocksRaycasts = false;
			group.interactable = false;

			// The round window: a disc that clips the map and is not drawn itself.
			Image clip = MakeImage("Window", root, Shapes.Disc(256, Color.white, Color.white, 0f));
			Stretch(clip.rectTransform);
			clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;

			mapImage = new GameObject("Track", typeof(RectTransform)).AddComponent<RawImage>();
			mapImage.raycastTarget = false;
			map = mapImage.rectTransform;
			map.SetParent(clip.rectTransform, false);
			map.anchorMin = map.anchorMax = map.pivot = new Vector2(0.5f, 0.5f);

			botDot = Shapes.Disc(32, BotColour, Outline, 4f);
			remoteDot = Shapes.Disc(32, RemoteColour, Outline, 4f);
			player = MakeImage("Player", map, Shapes.Arrow(64, PlayerColour, Outline, 6f));

			frame = MakeImage("Frame", root, Shapes.Ring(256, 9f, Color.white));
			Stretch(frame.rectTransform);
			root.gameObject.SetActive(false);
		}

		private static Image MakeImage(string name, Transform parent, Sprite sprite)
		{
			Image img = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
			img.raycastTarget = false;
			img.sprite = sprite;
			RectTransform r = img.rectTransform;
			r.SetParent(parent, false);
			r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
			return img;
		}

		private static void Stretch(RectTransform r)
		{
			r.anchorMin = Vector2.zero;
			r.anchorMax = Vector2.one;
			r.offsetMin = Vector2.zero;
			r.offsetMax = Vector2.zero;
		}
	}
}
