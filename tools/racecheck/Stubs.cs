// Minimal managed stand-ins for the APIs Race uses. In-game lifetime and networking still need a play test.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
	public class Object
	{
		public static T FindFirstObjectByType<T>() where T : class => null;
		public int GetInstanceID() => GetHashCode();
	}
	public class Component : Object
	{
		public GameObject gameObject;
		public Transform transform => gameObject.transform;
	}
	public class GameObject : Object
	{
		public bool activeInHierarchy = true;
		public readonly Transform transform = new Transform();
		private readonly Dictionary<string, Component> components = new Dictionary<string, Component>();
		public T Add<T>() where T : Component, new()
		{
			T component = new T { gameObject = this };
			components[typeof(T).Name] = component;
			return component;
		}
		public Component GetComponent(string name) => components.TryGetValue(name, out Component c) ? c : null;
		public T GetComponent<T>() where T : Component => GetComponent(typeof(T).Name) as T;
	}
	public class Transform
	{
		public Vector3 position;
		public Vector3 forward = new Vector3(0f, 0f, 1f);
		public Transform parent;
		public bool IsChildOf(Transform other) => this == other || (parent != null && parent.IsChildOf(other));
	}
	public struct Vector3
	{
		public float x, y, z;
		public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
	}
	public static class Time { public static int frameCount; public static float unscaledTime; }
	public static class Mathf
	{
		public const float Rad2Deg = 180f / (float)Math.PI;
		public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
		public static float Max(float a, float b) => Math.Max(a, b);
	}
}
namespace UnityEngine.SceneManagement { public struct Scene { public string name; } }
public class EnemyController : UnityEngine.Component { public bool alive = true; }
public class CarControllerNew : UnityEngine.Component { }
public class RemoteCar : UnityEngine.Component { }
public class TrackManager : UnityEngine.Object
{
	public CarControllerNew playerCarController;
	public LapManager lapManager;
	public List<EnemyController> Enemies = new List<EnemyController>();
}
public class LapManager
{
	public struct LeaderboardEntry { }
	public Dictionary<UnityEngine.GameObject, LeaderboardEntry> leaderboard = new Dictionary<UnityEngine.GameObject, LeaderboardEntry>();
	public UnityEngine.GameObject PlayerCar;
	public PathManager trackLine;
}
public class PathManager : UnityEngine.Object
{
	public List<UnityEngine.Vector3> sampledPoints;
	public float trackLength;
}
public class WeatherProfile : UnityEngine.Object { public string name; }
public static class WeatherGrip { public static WeatherProfile Current; }
public static class RunState { public static WeatherProfile ActiveWeather; }
namespace HalfGripMiniMap
{
	internal enum Marker { Bot, Remote }
	internal class Palette { public string Name; }
	internal static class Palettes
	{
		internal static Palette OpenWorld = new Palette();
		internal static Palette For(string scene, WeatherProfile weather, PathManager line) => OpenWorld;
	}
	internal static class Mod
	{
		internal class Setting { internal float Value = 2.5f; }
		internal static Setting ViewTiles = new Setting();
		internal static void Info(string message) { }
	}
	internal static class GpsScreen { internal static float WorldWiden = 1f; }
	internal class PixelMap { internal int Size, Room; }
	internal static class OpenWorldLink
	{
		internal class World { internal float TileSize = 250f; }
		internal static World Tiles = new World();
		internal static bool Active = true;
		internal static void Update(UnityEngine.Vector3 player, float reach) { }
	}
	internal class TrackImage
	{
		internal float Scale, Detail;
		internal static TrackImage Build(List<UnityEngine.Vector3> points, Palette palette, int size, int room, int zoom) => new TrackImage();
	}
}
