using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firefly
{
	internal class EffectEditor
	{
		public static EffectEditor Instance { get; private set; }

		public Vector3 effectDirection = Vector3.up;
		public float effectSpeed;
		public float effectState;

		string[] bodyConfigs;

		string currentBody;
		BodyConfig config;

		AtmoFxModule fxModule = null;

		// gui
		Vector2 ui_bodyListPosition;
		int ui_bodyChoice;

		public EffectEditor()
		{
			Instance = this;

			bodyConfigs = new string[1 + ConfigManager.Instance.bodyConfigs.Count];
			bodyConfigs[0] = "Default";
			for (int i = 0; i < ConfigManager.Instance.bodyConfigs.Count; i++)
			{
				bodyConfigs[i + 1] = ConfigManager.Instance.bodyConfigs.ElementAt(i).Key;
			}
		}

		void ApplyConfig()
		{
			if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = config;
			else ConfigManager.Instance.defaultConfig = config;
		}

		void ApplyCameraDirection()
		{
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			if (FlightCamera.fetch.mainCamera == null) return;

			effectDirection = vessel.transform.InverseTransformDirection(FlightCamera.fetch.mainCamera.transform.forward);
		}

		public Vector3 GetWorldDirection()
		{
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return Vector3.zero;

			return vessel.transform.TransformDirection(effectDirection);
		}

		public void Open()
		{
			Vessel vessel = FlightGlobals.ActiveVessel;
			if (vessel == null) return;
			fxModule = vessel.FindVesselModuleImplementing<AtmoFxModule>();
			if (fxModule == null) return;

			fxModule.doEffectEditor = true;
			if (!fxModule.isLoaded) fxModule.CreateVesselFx();
		}

		public void Gui(int id)
		{
			GUILayout.BeginVertical();

			// body selection
			ui_bodyListPosition = GUILayout.BeginScrollView(ui_bodyListPosition);
			int newChoice = GUILayout.SelectionGrid(ui_bodyChoice, bodyConfigs, Mathf.Min(bodyConfigs.Length, 3), GUILayout.Width(300f));

			if (newChoice != ui_bodyChoice)
			{
				ui_bodyChoice = newChoice;
				currentBody = bodyConfigs[newChoice];
			}

			GUILayout.EndScrollView();

			// effect configuration
			effectSpeed = GuiUtils.LabelSlider("Effect strength", effectSpeed, 0f, (float)ModSettings.I["strength_base"]);
			effectState = GuiUtils.LabelSlider("Effect state", effectState, 0f, 1f);

			if (GUILayout.Button("Align effects to camera")) ApplyCameraDirection();

			// saving
			GUILayout.Space(20);
			if (GUILayout.Button("Apply config")) ApplyConfig();

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();

			// 3d
			Transform camTransform = fxModule.fxVessel.airstreamCamera.transform;
			if (!fxModule.debugMode) DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.cyan);
		}

		public void Close()
		{
			fxModule.doEffectEditor = false;
		}
	}
}
