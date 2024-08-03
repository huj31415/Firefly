using KSP.UI.Screens;
using System.Linq;
using UnityEngine;

namespace AtmosphericFx
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class WindowManager : MonoBehaviour
	{
		ApplicationLauncherButton appButton;
		Rect windowPosition = new Rect(0, 0, 300, 100);

		bool uiHidden = false;
		bool appToggle = false;

		public void Start()
		{
			appButton = ApplicationLauncher.Instance.AddModApplication(
				OnApplicationTrue, 
				OnApplicationFalse, 
				null, null, null, null, 
				ApplicationLauncher.AppScenes.FLIGHT, 
				AssetLoader.Instance.iconTexture
			);

			GameEvents.onHideUI.Add(OnHideUi);
			GameEvents.onShowUI.Add(OnShowUi);
		}

		public void OnDestroy()
		{
			ApplicationLauncher.Instance.RemoveModApplication(appButton);

			GameEvents.onHideUI.Remove(OnHideUi);
			GameEvents.onShowUI.Remove(OnShowUi);
		}

		public void OnGUI()
		{
			if (uiHidden || !appToggle) return;

			windowPosition = GUILayout.Window(10, windowPosition, OnWindow, "Atmospheric Effects Configuration");
		}

		void OnApplicationTrue()
		{
			appToggle = true;
		}

		void OnApplicationFalse()
		{
			appToggle = false;
		}

		void OnHideUi()
		{
			uiHidden = true;
		}

		void OnShowUi()
		{
			uiHidden = false;
		}

		void OnWindow(int id)
		{
			// init
			Vessel vessel = FlightGlobals.ActiveVessel;
			AtmoFxModule fxModule = EventManager.fxInstances[vessel.id];

			// drawing
			GUILayout.BeginVertical();

			GUILayout.Label($"All assets loaded? {AssetLoader.Instance.allAssetsLoaded}");
			GUILayout.Space(20);

			GUILayout.Label($"Active vessel is {vessel.vesselName}");
			GUILayout.Label($"Current body: {vessel.mainBody.bodyName}");
			GUILayout.Label($"Vessel radius is {fxModule.fxVessel.vesselBoundRadius}");
			GUILayout.Label($"Vessel effect length multiplier is {fxModule.fxVessel.lengthMultiplier}");
			GUILayout.Label($"Vessel entry speed is {fxModule.GetAdjustedEntrySpeed()}");
			GUILayout.Space(20);

			GUILayout.Label($"AeroFX scalar is {fxModule.AeroFX.FxScalar}");
			GUILayout.Label($"AeroFX state is {fxModule.AeroFX.state}");
			GUILayout.Label($"AeroFX airspeed is {fxModule.AeroFX.airSpeed}");
			GUILayout.Space(20);

			GUILayout.Label("Current config:");
			GUILayout.Label($"{fxModule.currentBody.bodyName}");
			if (GUILayout.Button("Reload configs"))
			{
				Logging.Log("Reloading configs...");

				ConfigManager.ModuleManagerPostLoad();

				for (int i = 0; i < EventManager.fxInstances.Count; i++)
				{
					EventManager.fxInstances.ElementAt(i).Value.ReloadVessel();
				}
			}

			GUILayout.EndVertical();
			GUI.DragWindow();
		}
	}
}
