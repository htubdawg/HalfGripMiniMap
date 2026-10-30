using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfGripMiniMap
{
	/// <summary>
	/// The sound of the main menu's screen tap, for the GPS. The menu plays its clip <c>ScreenTap</c> from an audio source of its own
	/// (with a VariedSound, half its pitch either way), and no race scene has it, so it is looked for among the loaded assets at each
	/// scene load until found, which is the main menu at game start, and kept: a clip held in a static field is not unloaded with the
	/// menu. Not found, the GPS is tapped in silence.
	///
	/// The clip is much longer than one tap (22.8 s in game 0.418), and played to its end it went on making tap noises long after the
	/// poke (owner's test of 1.2.2). So only its first sound is played: where that ends is read off the clip's samples when it is found
	/// (<see cref="Measure"/>), and the GPS's own audio source is destroyed, which stops it, at that point.
	/// </summary>
	internal static class GpsTap
	{
		private const string ClipName = "ScreenTap";
		/// <summary>How far either side of its own pitch the sound may land: the menu's VariedSound.pitchJitter.</summary>
		private const float PitchJitter = 0.5f;
		/// <summary>Seconds played when the clip's samples cannot be read (a compressed or streamed clip): about one short tap.</summary>
		private const float FallbackLength = 0.4f;
		/// <summary>Envelope window, and how long a quiet stretch must last to end a sound, in seconds.</summary>
		private const float Window = 0.01f;
		private const float Gap = 0.15f;
		/// <summary>Share of the clip's loudest sample under which it counts as quiet.</summary>
		private const float Quiet = 0.1f;

		private static AudioClip clip;
		private static float length = FallbackLength;
		private static bool missed;

		internal static void OnSceneLoaded(Scene loaded) => Find();

		internal static void Find()
		{
			if (clip != null)
			{
				return;
			}
			try
			{
				foreach (AudioClip c in Resources.FindObjectsOfTypeAll<AudioClip>())
				{
					if (c != null && c.name == ClipName)
					{
						clip = c;
						Mod.Info("GPS: kept the main menu's tap sound '" + ClipName + "' (" + c.length.ToString("0.00") + " s, " + c.channels + " channel(s), " + c.frequency + " Hz, " + c.loadType + ", " + c.loadState + ")");
						Measure(c);
						return;
					}
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("GPS: could not look for the tap sound: " + e.Message);
			}
		}

		/// <summary>
		/// Finds where the clip's first sound ends: the loudness in <see cref="Window"/> steps, the first step over <see cref="Quiet"/> of
		/// the loudest, then the first quiet stretch of <see cref="Gap"/> after it. Also logs where every loud burst starts, which tells
		/// what the long clip holds. Unreadable samples leave <see cref="FallbackLength"/>.
		/// </summary>
		private static void Measure(AudioClip c)
		{
			try
			{
				int channels = Mathf.Max(1, c.channels);
				int frames = c.samples;
				float[] data = new float[frames * channels];
				if (frames <= 0 || !c.GetData(data, 0))
				{
					Mod.Info("GPS: the tap sound's samples cannot be read; playing its first " + FallbackLength + " s");
					return;
				}
				int step = Mathf.Max(1, Mathf.RoundToInt(c.frequency * Window));
				int windows = (frames + step - 1) / step;
				float[] loud = new float[windows];
				float peak = 0f;
				for (int i = 0; i < data.Length; i++)
				{
					float a = Mathf.Abs(data[i]);
					int w = i / channels / step;
					if (a > loud[w])
					{
						loud[w] = a;
					}
					peak = Mathf.Max(peak, a);
				}
				if (peak <= 0f)
				{
					Mod.Info("GPS: the tap sound is silent");
					return;
				}
				float quiet = peak * Quiet;
				int gap = Mathf.CeilToInt(Gap / Window);
				System.Collections.Generic.List<string> bursts = new System.Collections.Generic.List<string>();
				int firstEnd = -1;
				int run = gap;
				for (int w = 0; w < windows; w++)
				{
					if (loud[w] > quiet)
					{
						if (run >= gap)
						{
							bursts.Add((w * Window).ToString("0.00"));
						}
						run = 0;
					}
					else
					{
						run++;
						if (run == gap && bursts.Count > 0 && firstEnd < 0)
						{
							firstEnd = w - gap + 1;
						}
					}
				}
				length = Mathf.Clamp((firstEnd < 0 ? windows : firstEnd) * Window + 0.05f, 0.05f, c.length);
				Mod.Info("GPS: the tap sound has " + bursts.Count + " loud burst(s), starting at " + string.Join(", ", bursts.GetRange(0, Mathf.Min(20, bursts.Count))) + (bursts.Count > 20 ? ", ..." : "") + " s; playing its first " + length.ToString("0.00") + " s");
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("GPS: could not measure the tap sound (playing its first " + FallbackLength + " s): " + e.Message);
			}
		}

		/// <summary>Plays the first sound of the tap from <paramref name="on"/>, so it goes to the mixer group of the cockpit it is in.</summary>
		internal static void Play(GameObject on)
		{
			if (clip == null)
			{
				if (!missed)
				{
					missed = true;
					Mod.Info("GPS: the main menu's tap sound '" + ClipName + "' was not found; tapping in silence");
				}
				return;
			}
			float pitch = 1f + UnityEngine.Random.Range(-PitchJitter, PitchJitter);
			AudioSource source = SoundController.Create2DAudioSource(clip, on, 1f, pitch);
			if (source == null)
			{
				return;
			}
			source.Play();
			// Destroying the source stops it: the rest of the clip is never heard.
			UnityEngine.Object.Destroy(source.gameObject, length / pitch);
		}
	}

	/// <summary>
	/// The GPS screen as the cockpit hand sees it: the game's own click target (Grabber clicks the <see cref="Clickable"/> over the
	/// interior collider it hovers, left and right), so the hand shows its pointing pose over the screen and the gamepad hand snaps to it.
	/// HalfGripTools has no interaction API of its own; every mod's cockpit parts go through Grabber like this.
	/// </summary>
	internal sealed class GpsButton : Clickable
	{
		/// <summary>
		/// Grabber's hover goes to the highest sortingLayer under the hand, then the nearest. The game's controls use -1 to 2 (the revolver's
		/// barrel -1, levers 0, the claxon 1, the shotgun's pump 2); the GPS is below them all, so a lever, a gun or a magazine over or
		/// beside the screen always wins and the GPS only gets the hand when nothing else wants it.
		/// </summary>
		internal const int SortingLayer = -100;

		internal Action Left;
		internal Action Right;

		protected override void OnClick() => Left?.Invoke();

		protected override void OnRightClick() => Right?.Invoke();
	}
}
