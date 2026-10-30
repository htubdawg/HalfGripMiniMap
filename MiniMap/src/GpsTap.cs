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
	/// </summary>
	internal static class GpsTap
	{
		private const string ClipName = "ScreenTap";
		/// <summary>How far either side of its own pitch the sound may land: the menu's VariedSound.pitchJitter.</summary>
		private const float PitchJitter = 0.5f;

		private static AudioClip clip;
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
						Mod.Info("GPS: kept the main menu's tap sound '" + ClipName + "' (" + c.length.ToString("0.00") + " s)");
						return;
					}
				}
			}
			catch (Exception e)
			{
				Mod.Log.LogWarning("GPS: could not look for the tap sound: " + e.Message);
			}
		}

		/// <summary>Plays the tap from <paramref name="on"/>, so it goes to the mixer group of the cockpit it is in.</summary>
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
			SoundController.PlaySound2DPitch(clip, on, 1f + UnityEngine.Random.Range(-PitchJitter, PitchJitter));
		}
	}

	/// <summary>
	/// The GPS screen as the cockpit hand sees it: the game's own click target (Grabber clicks the <see cref="Clickable"/> over the
	/// interior collider it hovers, left and right), so the hand shows its pointing pose over the screen and the gamepad hand snaps to it.
	/// </summary>
	internal sealed class GpsButton : Clickable
	{
		internal Action Left;
		internal Action Right;

		protected override void OnClick() => Left?.Invoke();

		protected override void OnRightClick() => Right?.Invoke();
	}
}
