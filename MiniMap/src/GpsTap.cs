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
	/// HalfGripTools has no interaction API of its own; every mod's cockpit parts go through Grabber like this.
	///
	/// A click only counts while the hand is locked onto the GPS: Grabber hovers it, the hand shows its pointing pose
	/// (<see cref="HandState.Interactible"/>), and has for <see cref="LockSeconds"/>. A click on the way past, or one meant for a control
	/// the screen sits near, does nothing. Its collider is also put below every control the game has (see <see cref="SortingLayer"/>).
	/// </summary>
	internal sealed class GpsButton : Clickable
	{
		/// <summary>
		/// Grabber's hover goes to the highest sortingLayer under the hand, then the nearest. The game's controls use -1 to 2 (the revolver's
		/// barrel -1, levers 0, the claxon 1, the shotgun's pump 2); the GPS is below them all, so a lever, a gun or a magazine over or
		/// beside the screen always wins and the GPS only gets the hand when nothing else wants it.
		/// </summary>
		internal const int SortingLayer = -100;
		/// <summary>Seconds the hand must have been on the GPS before a click counts: about how long it takes to snap on (Grabber.handMagnetSmoothing).</summary>
		private const float LockSeconds = 0.1f;

		internal Action Left;
		internal Action Right;

		private Grabber grabber;
		private float nextSearch;
		private float hoveredSince = float.PositiveInfinity;

		/// <summary>True while the hand is locked onto the GPS.</summary>
		private bool Locked => grabber != null && grabber.Hovered == attachedCollider && grabber.CurrentHandState == HandState.Interactible && Time.unscaledTime - hoveredSince >= LockSeconds;

		private void Update()
		{
			if (grabber == null && Time.unscaledTime >= nextSearch)
			{
				nextSearch = Time.unscaledTime + 1f;
				grabber = FindFirstObjectByType<Grabber>();
			}
			if (grabber == null || attachedCollider == null || grabber.Hovered != attachedCollider)
			{
				hoveredSince = float.PositiveInfinity;
			}
			else if (float.IsPositiveInfinity(hoveredSince))
			{
				hoveredSince = Time.unscaledTime;
			}
		}

		protected override void OnClick() => Pressed(Left, "left");

		protected override void OnRightClick() => Pressed(Right, "right");

		private void Pressed(Action action, string which)
		{
			if (!Locked)
			{
				Mod.Info("GPS: " + which + " click ignored, the hand is not locked onto the screen (" + (grabber != null ? grabber.CurrentHandState.ToString() : "no hand") + ")");
				return;
			}
			action?.Invoke();
		}
	}
}
