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
		Rect settingsWindowPosition = new Rect(600, 100, 300, 100);
		Rect windowPosition = new Rect(0, 100, 300, 100);
		Rect infoWindowPosition = new Rect(300, 100, 300, 100);

		bool uiHidden = false;
		bool appToggle = false;

		// override toggle values
		public bool tgl_EffectToggle = true;

		// timer
		float reloadBtnTime = 0f;

		// effect editor
		EffectEditor effectEditor;
		Rect effectEditorPosition = new Rect(900, 100, 300, 100);
		bool effectEditorActive = false;
		
		public void Awake()
		{
			Instance = this;

			effectEditor = new EffectEditor();
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

			settingsWindowPosition = GUILayout.Window(511, settingsWindowPosition, OnSettingsWindow, "Firefly Settings");
			windowPosition = GUILayout.Window(416, windowPosition, OnWindow, "Quick actions");
			infoWindowPosition = GUILayout.Window(410, infoWindowPosition, OnInfoWindow, "Info");

			if (effectEditorActive) effectEditorPosition = GUILayout.Window(512, effectEditorPosition, effectEditor.Gui, "Effect editor");
		}

		/// <summary>
		/// Config and override window
		/// </summary>
		void OnSettingsWindow(int id)
		{
			// drawing
			GUILayout.BeginVertical();

			GUILayout.Label("Fields that need a reload to update are marked with *");

			// draw config fields
			for (int i = 0; i < ModSettings.I.fields.Count; i++)
			{
				KeyValuePair<string, ModSettings.Field> field = ModSettings.I.fields.ElementAt(i);

				if (field.Value.valueType == ModSettings.ValueType.Boolean) GuiUtils.DrawConfigFieldBool(field.Key, ModSettings.I.fields);
				else if (field.Value.valueType == ModSettings.ValueType.Float) GuiUtils.DrawConfigFieldFloat(field.Key, ModSettings.I.fields);
			}

			GUILayout.Space(20);
			if (GUILayout.Button("Save overrides to file")) SettingsManager.Instance.SaveModSettings();

			GUILayout.Space(20);
			if (GUILayout.Button($"{(effectEditorActive ? "Close" : "Open")} effect editor"))
			{
				effectEditorActive = !effectEditorActive;
				if (effectEditorActive) effectEditor.Open();
				else effectEditor.Close();
			}

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		/// <summary>
		/// Quick actions window
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
			
			GUILayout.Space(20);
			if (GUILayout.Button($"Toggle effects {(tgl_EffectToggle ? "(TURN OFF)" : "(TURN ON)")}")) tgl_EffectToggle = !tgl_EffectToggle;
			if (GUILayout.Button($"Toggle debug vis {(fxModule.debugMode ? "(TURN OFF)" : "(TURN ON)")}")) fxModule.debugMode = !fxModule.debugMode;

			GUILayout.Space(20);
			if (GUILayout.Button("Reload assetbundle")) AssetLoader.Instance.ReloadAssets();

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

			GUILayout.Label($"Mod version: beta-{Versioning.Version}. This is a testing-only build.");
			GUILayout.Label($"All assets loaded? {AssetLoader.Instance.allAssetsLoaded}");
			GUILayout.Space(20);

			GUILayout.Label($"Active vessel is {vessel.vesselName}");
			GUILayout.Label($"Vessel radius is {fxModule.fxVessel.vesselBoundRadius}");
			if (!fxModule.doEffectEditor) GUILayout.Label($"Entry strength is {fxModule.GetAdjustedEntrySpeed()}");
			GUILayout.Space(20);

			GUILayout.Label($"Current config is {fxModule.currentBody.bodyName}");

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}
	}
}
