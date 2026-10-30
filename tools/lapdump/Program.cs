using System;
using System.Globalization;
using System.IO;

namespace HalfGripProcTrack
{
	/// <summary>What Layout.cs logs through, outside the game.</summary>
	internal static class Mod
	{
		internal sealed class Logger
		{
			public void LogWarning(string text) => Console.Error.WriteLine(text);
			public void LogInfo(string text) { }
		}

		internal static Logger Log = new Logger();
		internal static void Info(string text) { }
	}

	internal static class Program
	{
		/// <summary>
		/// Writes lap_&lt;seed&gt;.csv (x,z per point, about 5 m apart) into the out folder for each seed: real ProcTrack laps (its
		/// Generator.Make, the Stadium circuit's shape), for tools/preview. Layout.cs needs only Unity's managed math, so it runs here.
		///   dotnet run -c Release --project tools/lapdump [-p:ProcTrack=&lt;repo&gt;] [-p:Refs=&lt;HalfGripRefs&gt;] -- &lt;out dir&gt; &lt;seed&gt;...
		/// The repos default to siblings of this one.
		/// </summary>
		private static void Main(string[] args)
		{
			string dir = args[0];
			Directory.CreateDirectory(dir);
			for (int a = 1; a < args.Length; a++)
			{
				int seed = int.Parse(args[a]);
				Layout lap = Generator.Make(seed);
				using StreamWriter w = new StreamWriter(Path.Combine(dir, "lap_" + seed + ".csv"));
				foreach (UnityEngine.Vector3 p in lap.Points)
				{
					w.WriteLine(p.x.ToString(CultureInfo.InvariantCulture) + "," + p.z.ToString(CultureInfo.InvariantCulture));
				}
				Console.WriteLine(seed + ": " + lap.Points.Length + " points, " + lap.Length.ToString("0") + " m" + (lap.Fallback ? " (fallback)" : ""));
			}
		}
	}
}
