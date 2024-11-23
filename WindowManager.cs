using KSP.UI.Screens;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class WindowManager : MonoBehaviour
	{
		public static WindowManager Instance { get; private set; }

		ApplicationLauncherButton appButton;
		Rect windowPosition = new Rect(0, 100, 300, 100);
		Rect infoWindowPosition = new Rect(300, 100, 300, 100);

		bool uiHidden = false;
		bool appToggle = false;

		// override toggle values
		public Dictionary<string, bool> configToggles = new Dictionary<string, bool>();

		public bool tgl_SpeedMethod = false;
		public bool tgl_EffectToggle = true;

		// timer
		float reloadBtnTime = 0f;
		
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

		/// <summary>
		/// Initializes the default values of the toggles with the values loaded from the cfg file
		/// </summary>
		public void InitializeDefaultValues()
		{
			configToggles["hdr_override"] = (bool)ConfigManager.Instance.modSettings["hdr_override"];
			configToggles["use_colliders"] = (bool)ConfigManager.Instance.modSettings["use_colliders"];
			configToggles["disable_particles"] = (bool)ConfigManager.Instance.modSettings["disable_particles"];
			configToggles["disable_sparks"] = (bool)ConfigManager.Instance.modSettings["disable_sparks"];
			configToggles["disable_debris"] = (bool)ConfigManager.Instance.modSettings["disable_debris"];
			configToggles["disable_smoke"] = (bool)ConfigManager.Instance.modSettings["disable_smoke"];
		}

		public void OnDestroy()
		{
			// remove everything associated with the thing

			ApplicationLauncher.Instance.RemoveModApplication(appButton);

			GameEvents.onHideUI.Remove(OnHideUi);
			GameEvents.onShowUI.Remove(OnShowUi);
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

		public void Update()
		{
			
		}

		public void OnGUI()
		{
			if (uiHidden || !appToggle || FlightGlobals.ActiveVessel == null) return;

			windowPosition = GUILayout.Window(416, windowPosition, OnWindow, "Atmospheric Effects Configuration");
			infoWindowPosition = GUILayout.Window(410, infoWindowPosition, OnInfoWindow, "Atmospheric Effects Info");
		}

		/// <summary>
		/// Config and override window
		/// </summary>
		void OnWindow(int id)
		{
			// init
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			var fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
			if (fxModule == null) return;

			if (!fxModule.isLoaded)
			{
				GUILayout.BeginVertical();
				GUILayout.Label("FX are not loaded for the active vessel");
				GUILayout.EndVertical();
				GUI.DragWindow();
				return;
			}

			// drawing
			GUILayout.BeginVertical();

			bool canReload = (Time.realtimeSinceStartup - reloadBtnTime) > 1f;
			if (GUILayout.Button("Reload Vessel") && canReload)
			{
				fxModule.ReloadVessel();
				reloadBtnTime = Time.realtimeSinceStartup;
			}

			// draw config fields
			for (int i = 0; i < ConfigManager.Instance.modSettings.fields.Count; i++)
			{
				KeyValuePair<string, ModSettings.Field> field = ConfigManager.Instance.modSettings.fields.ElementAt(i);
				if (field.Value.valueType != ModSettings.ValueType.Boolean) continue;

				if (DrawConfigField(field.Key, configToggles)) ApplyOverride(field.Key);
			}

			// button to apply all overrides
			if (GUILayout.Button("Apply all overrides"))
			{
				for (int i = 0; i < configToggles.Keys.Count; i++)
				{
					ApplyOverride(configToggles.Keys.ElementAt(i));
				}
			}
			
			// other configs
			GUILayout.Space(20);
			DrawConfigField("Speed method", ref tgl_SpeedMethod);
			if (GUILayout.Button("Save overrides")) ConfigManager.Instance.SaveModSettings();
			if (GUILayout.Button($"Toggle effects {(tgl_EffectToggle ? "(TURN OFF)" : "(TURN ON)")}")) tgl_EffectToggle = !tgl_EffectToggle;

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Info window
		/// </summary>
		void OnInfoWindow(int id)
		{
			// init
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			var fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
			if (fxModule == null) return;

			if (!fxModule.isLoaded)
			{
				GUILayout.BeginVertical();
				GUILayout.Label("FX are not loaded for the active vessel");
				GUILayout.EndVertical();
				GUI.DragWindow();
				return;
			}

			// drawing
			GUILayout.BeginVertical();

			GUILayout.Label($"Current vessel loaded? {fxModule.isLoaded}");
			GUILayout.Label($"Mod version: beta-{Versioning.Version}. This is a testing-only build.");
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

			GUILayout.Label($"Current config is {fxModule.currentBody.bodyName}");
			GUILayout.Space(20);

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Draws a config field with a toggle switch and a button which applies it
		/// </summary>
		/// <param name="label">Label to show</param>
		/// <param name="tgl">The value of the toggle switch</param>
		/// <returns>The apply button state</returns>
		bool DrawConfigField(string label, ref bool tgl)
		{
			GUILayout.BeginHorizontal();
				tgl = GUILayout.Toggle(tgl, label);
				bool result = GUILayout.Button("Apply override");
			GUILayout.EndHorizontal();

			return result;
		}

		/// <summary>
		/// Draws a config field with a toggle switch and a button which applies it
		/// This variant uses a dict instead of a toggle reference
		/// </summary>
		/// <param name="label">Label to show</param>
		/// <param name="tgl">The dict contatining the toggle values</param>
		/// <returns>The apply button state</returns>
		bool DrawConfigField(string label, Dictionary<string, bool> tgl)
		{
			GUILayout.BeginHorizontal();
			tgl[label] = GUILayout.Toggle(tgl[label], label);
			bool result = GUILayout.Button("Apply override");
			GUILayout.EndHorizontal();

			return result;
		}
		
		void ApplyOverride(string key)
		{
			bool value = configToggles[key];
			ConfigManager.Instance.modSettings[key] = value;

			// special cases
			if (key == "hdr_override") CameraManager.Instance.OverrideHDR(value);
		}
	}
}
