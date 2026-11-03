using System;
using HalfGripMiniMap;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class Program
{
	private static int passed;

	private static void Check(string name, params (float x, Marker marker)[] expected)
	{
		Time.frameCount++;
		Race.Tick();
		if (!Race.Ready || Race.Cars.Count != expected.Length)
		{
			throw new Exception(name + ": expected " + expected.Length + " markers, got " + Race.Cars.Count);
		}
		foreach ((float x, Marker marker) in expected)
		{
			int found = Race.Cars.FindAll(c => c.Key.x == x && c.Value == marker).Count;
			if (found != 1) throw new Exception(name + ": missing or duplicate " + marker + " at " + x);
		}
		Console.WriteLine("PASS " + name);
		passed++;
	}

	private static GameObject Enemy(float x, bool remote = false)
	{
		GameObject car = new GameObject();
		car.transform.position = new Vector3(x, 0f, 0f);
		car.Add<EnemyController>();
		if (remote) car.Add<RemoteCar>();
		return car;
	}

	private static void Main()
	{
		Race.OnSceneLoaded(new Scene { name = "test" });
		GameObject self = new GameObject();
		LapManager laps = new LapManager { PlayerCar = self };
		TrackManager manager = new TrackManager { lapManager = laps, playerCarController = self.Add<CarControllerNew>() };
		Race.Manager = manager;
		laps.leaderboard.Add(self, default);
		GameObject original = Enemy(10f, true);
		laps.leaderboard.Add(original, default);
		manager.Enemies.Add(original.GetComponent<EnemyController>());
		Check("initial remote in both lists appears once", (10f, Marker.Remote));

		original.GetComponent<EnemyController>().alive = false;
		laps.leaderboard.Remove(original); // LapManager prunes the wreck; the local player remains registered.
		GameObject fresh = Enemy(20f, true);
		manager.Enemies.Add(fresh.GetComponent<EnemyController>());
		Check("respawn absent from nonempty leaderboard appears blue", (20f, Marker.Remote));
		Check("next frame retains marker", (20f, Marker.Remote));
		laps.leaderboard.Add(original, default);
		Check("wreck still active and listed has no marker", (20f, Marker.Remote));
		laps.leaderboard.Remove(original);

		fresh.GetComponent<EnemyController>().alive = false;
		GameObject again = Enemy(30f, true);
		manager.Enemies.Add(again.GetComponent<EnemyController>());
		Check("second respawn replaces the wreck", (30f, Marker.Remote));
		laps.leaderboard.Add(again, default);
		Check("late leaderboard registration does not duplicate", (30f, Marker.Remote));

		GameObject bot = Enemy(40f);
		manager.Enemies.Add(bot.GetComponent<EnemyController>());
		Check("ordinary enemy remains a bot", (30f, Marker.Remote), (40f, Marker.Bot));
		manager.Enemies.Add(null);
		manager.Enemies.Add(bot.GetComponent<EnemyController>());
		GameObject inactive = Enemy(50f, true);
		inactive.activeInHierarchy = false;
		manager.Enemies.Add(inactive.GetComponent<EnemyController>());
		laps.leaderboard.Add(inactive, default);
		GameObject parent = Enemy(60f, true);
		self.transform.parent = parent.transform;
		manager.Enemies.Add(parent.GetComponent<EnemyController>());
		self.Add<EnemyController>();
		manager.Enemies.Add(self.GetComponent<EnemyController>());
		Check("null, inactive, duplicate and local cars excluded", (30f, Marker.Remote), (40f, Marker.Bot));

		laps.leaderboard.Clear();
		Check("empty leaderboard keeps remote colour", (30f, Marker.Remote), (40f, Marker.Bot));
		manager.lapManager = null;
		Check("missing lap manager still gathers live enemies", (30f, Marker.Remote), (40f, Marker.Bot));
		manager.lapManager = laps;
		manager.Enemies = null;
		laps.leaderboard.Add(again, default);
		Check("leaderboard alone still works", (30f, Marker.Remote));
		Race.OnSceneLoaded(new Scene { name = "next" });
		if (Race.Cars.Count != 0) throw new Exception("scene change kept markers");
		Race.Manager = manager;
		Check("scene change clears collection state", (30f, Marker.Remote));
		Console.WriteLine(passed + " checks passed");
	}
}
