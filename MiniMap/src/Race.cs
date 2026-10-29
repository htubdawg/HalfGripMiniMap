using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The race the maps show, shared by the screen minimap and the GPS on the dash: the scene's <see cref="TrackManager"/>, its track
	/// line and the colours for it, the player's car and every other car. Whoever draws first in a frame brings it up to date
	/// (<see cref="Tick"/>); the rest read it. A map mod may build or replace the track line some time after the scene loads (ProcTrack
	/// does, in its own scene callback), so the line is checked twice a second, and <see cref="Version"/> goes up whenever the lap or its
	/// colours change so each map knows to draw the lap again.
	/// </summary>
	internal static class Race
	{
		internal static string Scene = "";
		internal static TrackManager Manager;
		internal static PathManager Line;
		internal static Palette Palette;
		/// <summary>Goes up each time the lap or its colours change.</summary>
		internal static int Version;
		/// <summary>Degrees clockwise from north that the player's car points.</summary>
		internal static float Heading;
		internal static Vector3 Player;
		internal static readonly List<KeyValuePair<Vector3, Marker>> Cars = new List<KeyValuePair<Vector3, Marker>>();

		private static int tickedFrame = -1;
		private static float nextSearch;
		private static float nextCheck;
		private static string drawn;

		/// <summary>True while there is a race with a lap and a player car to show.</summary>
		internal static bool Ready { get; private set; }

		internal static void OnSceneLoaded(Scene loaded)
		{
			Scene = loaded.name;
			Manager = null;
			Line = null;
			Palette = null;
			drawn = null;
			nextSearch = 0f;
			Ready = false;
			Cars.Clear();
		}

		/// <summary>Brings the race up to date, once per frame however many maps ask.</summary>
		internal static void Tick()
		{
			if (tickedFrame == Time.frameCount)
			{
				return;
			}
			tickedFrame = Time.frameCount;
			Ready = Find() && Manager.playerCarController != null;
			if (Ready)
			{
				Gather();
			}
		}

		/// <summary>Finds the race and keeps the lap and its colours in step with its track line.</summary>
		private static bool Find()
		{
			if (Manager == null)
			{
				if (Time.unscaledTime < nextSearch)
				{
					return false;
				}
				nextSearch = Time.unscaledTime + 1f;
				Manager = Object.FindFirstObjectByType<TrackManager>();
				if (Manager == null)
				{
					return false;
				}
				Mod.Info("Minimap: race found in scene " + Scene);
			}
			PathManager line = Manager.lapManager != null ? Manager.lapManager.trackLine : null;
			if (line == null || line.sampledPoints == null || line.sampledPoints.Count < 8)
			{
				return false;
			}
			if (Palette == null || line != Line || Time.unscaledTime >= nextCheck)
			{
				nextCheck = Time.unscaledTime + 0.5f;
				WeatherProfile weather = WeatherGrip.Current ?? RunState.ActiveWeather;
				string now = Signature(line, weather);
				if (now != drawn)
				{
					Line = line;
					Palette = Palettes.For(Scene, weather, line);
					drawn = now;
					Version++;
					Mod.Info("Minimap: a " + line.trackLength.ToString("0") + " m lap of " + line.sampledPoints.Count + " points in " + Scene + ", colours " + Palette.Name + (weather != null ? " (weather " + weather.name + ")" : ""));
				}
			}
			return true;
		}

		/// <summary>What the lap was last drawn from: the line's points and length, and the weather that coloured it.</summary>
		private static string Signature(PathManager line, WeatherProfile weather)
		{
			List<Vector3> pts = line.sampledPoints;
			return pts.Count + "|" + pts[0] + "|" + pts[pts.Count / 3] + "|" + pts[2 * pts.Count / 3] + "|" + line.trackLength.ToString("0.0") + "|" + (weather != null ? weather.GetInstanceID() : 0);
		}

		/// <summary>The player's heading and position, and every other car in the race.</summary>
		private static void Gather()
		{
			Transform car = Manager.playerCarController.transform;
			Vector3 forward = car.forward;
			Heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
			Player = car.position;

			Cars.Clear();
			Dictionary<GameObject, LapManager.LeaderboardEntry> board = Manager.lapManager.leaderboard;
			if (board != null && board.Count > 0)
			{
				GameObject self = Manager.lapManager.PlayerCar;
				foreach (GameObject other in board.Keys)
				{
					if (other != null && other != self && other.activeInHierarchy && other.transform != car && !car.IsChildOf(other.transform))
					{
						Cars.Add(new KeyValuePair<Vector3, Marker>(other.transform.position, other.GetComponent("RemoteCar") != null ? Marker.Remote : Marker.Bot));
					}
				}
			}
			else if (Manager.Enemies != null)
			{
				foreach (EnemyController enemy in Manager.Enemies)
				{
					if (enemy != null && enemy.gameObject.activeInHierarchy)
					{
						Cars.Add(new KeyValuePair<Vector3, Marker>(enemy.transform.position, Marker.Bot));
					}
				}
			}
		}
	}

	/// <summary>One map's picture of the lap at its own size, drawn again when the race's lap or colours change.</summary>
	internal sealed class LapCache
	{
		private TrackImage image;
		private int version = -1;
		private int size;
		private int room;

		/// <summary>The lap for a map <paramref name="mapSize"/> pixels wide with <paramref name="mapRoom"/> pixels kept clear at its rim.</summary>
		internal TrackImage Get(int mapSize, int mapRoom)
		{
			if (image == null || version != Race.Version || size != mapSize || room != mapRoom)
			{
				image = TrackImage.Build(Race.Line.sampledPoints, Race.Palette, mapSize, mapRoom);
				version = Race.Version;
				size = mapSize;
				room = mapRoom;
				Mod.Info("Minimap: drew the lap " + mapSize + " px wide, " + (image.Scale / TrackImage.Detail).ToString("0.000") + " px/m");
			}
			return image;
		}
	}
}
