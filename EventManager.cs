using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class EventManager : MonoBehaviour
	{
		public void Start()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			GameEvents.onVesselPartCountChanged.Add(OnVesselPartCountChanged);
			GameEvents.onVesselSOIChanged.Add(OnVesselSOIChanged);
			GameEvents.OnCameraChange.Add(OnCameraChange);
		}

		public void OnDestroy()
		{
			GameEvents.onVesselPartCountChanged.Remove(OnVesselPartCountChanged);
			GameEvents.onVesselSOIChanged.Remove(OnVesselSOIChanged);
			GameEvents.OnCameraChange.Remove(OnCameraChange);
		}

		void OnCameraChange(global::CameraManager.CameraMode mode)
		{
			CameraManager.Instance.OnCameraChange(mode);
		}

		/// <summary>
		/// Fires everytime a vessel is modified, sends a reload event
		/// </summary>
		void OnVesselPartCountChanged(Vessel vessel)
		{
			Logging.Log($"Modified vessel {vessel.name}");

			var module = vessel.FindVesselModuleImplementing<AtmoFxModule>();

			if (module != null) module.OnVesselPartCountChanged();
			else Logging.Log("FX instance not registered");
		}

		/// <summary>
		/// Fires everytime a vessel changes it's SOI
		/// </summary>
		void OnVesselSOIChanged(GameEvents.HostedFromToAction<Vessel, CelestialBody> action)
		{
			var module = action.host.FindVesselModuleImplementing<AtmoFxModule>();

			if (module != null)
			{
				module.OnVesselSOIChanged(action.to);
			}
		}
	}
}
