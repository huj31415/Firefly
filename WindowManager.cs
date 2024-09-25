using KSP.UI.Screens;
using UnityEngine;
using UnityEngine.Events;

namespace AtmosphericFx
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class WindowManager : MonoBehaviour
	{
		public static WindowManager Instance { get; private set; }

		ApplicationLauncherButton appButton;
		Rect windowPosition = new Rect(0, 0, 300, 100);

		bool uiHidden = false;
		bool appToggle = false;

		// Toggle values
		public bool tgl_Hdr = false;
		public bool tgl_UseColliders = false;
		public bool tgl_DisableParticles = false;
		
		public void Awake()
		{
			Instance = this;

			InitializeDefaultValues();
		}

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

		public void InitializeDefaultValues()
		{
			tgl_Hdr = ConfigManager.Instance.modSettings.hdrOverride;
			tgl_UseColliders = ConfigManager.Instance.modSettings.useColliders;
			tgl_DisableParticles = ConfigManager.Instance.modSettings.disableParticles;
		}

		public void OnDestroy()
		{
			ApplicationLauncher.Instance.RemoveModApplication(appButton);

			GameEvents.onHideUI.Remove(OnHideUi);
			GameEvents.onShowUI.Remove(OnShowUi);
		}

		public void OnGUI()
		{
			if (uiHidden || !appToggle || FlightGlobals.ActiveVessel == null) return;

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
			GUILayout.Label($"Vessel radius is {fxModule.fxVessel.vesselBoundRadius}");
			GUILayout.Label($"Effect length multiplier is {fxModule.fxVessel.lengthMultiplier}");
			GUILayout.Label($"Final entry speed is {fxModule.GetAdjustedEntrySpeed()}");
			GUILayout.Space(20);

			GUILayout.Label($"AeroFX scalar is {fxModule.AeroFX.FxScalar}");
			GUILayout.Label($"AeroFX state is {fxModule.AeroFX.state}");
			GUILayout.Label($"AeroFX airspeed is {fxModule.AeroFX.airSpeed}");
			GUILayout.Space(20);

			GUILayout.Label("Current config:");
			GUILayout.Label($"{fxModule.currentBody.bodyName}");
			GUILayout.Space(20);

			if (GUILayout.Button("Reload Vessel")) fxModule.ReloadVessel();
			if (DrawConfigField("HDR Override", ref tgl_Hdr)) CameraManager.Instance.OverrideHDR(tgl_Hdr);
			if (DrawConfigField("Use colliders", ref tgl_UseColliders)) ConfigManager.Instance.modSettings.useColliders = tgl_UseColliders;
			if (DrawConfigField("Disable particles", ref tgl_DisableParticles)) ConfigManager.Instance.modSettings.disableParticles = tgl_DisableParticles;
			if (GUILayout.Button("Save overrides")) ConfigManager.Instance.SaveModSettings();

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		bool DrawConfigField(string label, ref bool tgl)
		{
			GUILayout.BeginHorizontal();
				tgl = GUILayout.Toggle(tgl, label);
				bool result = GUILayout.Button("Apply override");
			GUILayout.EndHorizontal();

			return result;
		}
	}
}
