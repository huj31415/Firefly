using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firefly
{
	internal class EffectEditor
	{
		public static EffectEditor Instance { get; private set; }

		public Vector3 effectDirection = -Vector3.up;
		public float effectSpeed;
		public float effectState;

		public BodyConfig config;

		string[] bodyConfigs;
		string currentBody;

		AtmoFxModule fxModule = null;

		// gui
		Vector2 ui_bodyListPosition;
		int ui_bodyChoice;

		string ui_strengthMultiplier;
		string ui_lengthMultiplier;
		string ui_opacityMultiplier;
		string ui_wrapFresnelModifier;
		string ui_particleThreshold;

		public ColorPickerWindow colorPicker;

		public EffectEditor()
		{
			Instance = this;

			bodyConfigs = ConfigManager.Instance.bodyConfigs.Keys.ToArray();

			colorPicker = new ColorPickerWindow("C", 900, 100, Color.red);
			colorPicker.show = true;
		}

		void SaveConfig()
		{
			if (currentBody != "Default") ConfigManager.Instance.bodyConfigs[currentBody] = config;
			else ConfigManager.Instance.defaultConfig = config;
		}

		void ResetFieldText()
		{
			ui_strengthMultiplier = config.strengthMultiplier.ToString();
			ui_lengthMultiplier = config.lengthMultiplier.ToString();
			ui_opacityMultiplier = config.opacityMultiplier.ToString();
			ui_wrapFresnelModifier = config.wrapFresnelModifier.ToString();
			ui_particleThreshold = config.particleThreshold.ToString();
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

			// select main body
			ui_bodyChoice = bodyConfigs.IndexOf(vessel.mainBody.bodyName);
			currentBody = vessel.mainBody.bodyName;
			config = new BodyConfig(ConfigManager.Instance.bodyConfigs[currentBody]);
			ResetFieldText();

			// load effects
			fxModule.doEffectEditor = true;
			if (!fxModule.isLoaded) fxModule.CreateVesselFx();
		}

		public void Close()
		{
			fxModule.doEffectEditor = false;

			fxModule.currentBody = ConfigManager.Instance.bodyConfigs[currentBody];
		}

		public void Gui(int id)
		{
			// split editor into 2 parts
			GUILayout.BeginHorizontal();

			// draw left part
			DrawLeftEditor();

			// draw right part
			DrawRightEditor();

			// end window
			GUILayout.EndHorizontal();
			GUI.DragWindow();

			// apply stuff
			fxModule.currentBody = config;

			// 3d
			if (fxModule == null || fxModule.fxVessel == null) return;
			Transform camTransform = fxModule.fxVessel.airstreamCamera.transform;
			if (!fxModule.debugMode) DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.cyan);
		}

		void DrawLeftEditor()
		{
			GUILayout.BeginVertical();

			// body selection
			DrawConfigSelector();
			GUILayout.Space(20);

			// sim configuration
			DrawSimConfiguration();
			GUILayout.Space(20);

			// saving
			if (GUILayout.Button("Align effects to camera")) ApplyCameraDirection();
			if (GUILayout.Button("Save to cfg file")) SaveConfig();

			// end
			GUILayout.EndVertical();
		}

		void DrawRightEditor()
		{
			GUILayout.BeginVertical();
			GUILayout.Label("Body configuration:");

			// body configuration
			DrawBodyConfiguration();

			// end
			GUILayout.EndVertical();
		}

		void DrawConfigSelector()
		{
			GUILayout.Label("Select a config:");
			ui_bodyListPosition = GUILayout.BeginScrollView(ui_bodyListPosition, GUILayout.Width(300f), GUILayout.Height(125f));
			int newChoice = GUILayout.SelectionGrid(ui_bodyChoice, bodyConfigs, Mathf.Min(bodyConfigs.Length, 3));

			if (newChoice != ui_bodyChoice)
			{
				ui_bodyChoice = newChoice;
				currentBody = bodyConfigs[newChoice];

				config = new BodyConfig(ConfigManager.Instance.bodyConfigs[currentBody]);
				ResetFieldText();

				fxModule.ReloadVessel();
			}

			GUILayout.EndScrollView();
		}

		void DrawBodyConfiguration()
		{
			GuiUtils.DrawFloatInput("Strength multiplier", ref ui_strengthMultiplier, ref config.strengthMultiplier, GUILayout.Width(300f));
			GuiUtils.DrawFloatInput("Length multiplier", ref ui_lengthMultiplier, ref config.lengthMultiplier);
			GuiUtils.DrawFloatInput("Opacity multiplier", ref ui_opacityMultiplier, ref config.opacityMultiplier);
			GuiUtils.DrawFloatInput("Wrap fresnel modifier", ref ui_wrapFresnelModifier, ref config.wrapFresnelModifier);
			GuiUtils.DrawFloatInput("Particle threshold", ref ui_particleThreshold, ref config.particleThreshold);

			config.streakProbability = GuiUtils.LabelSlider("Streak probability", config.streakProbability, 0f, 0.09f);
			config.streakThreshold = GuiUtils.LabelSlider("Streak threshold", config.streakThreshold, 0f, -0.2f);
		}

		void DrawSimConfiguration()
		{
			effectSpeed = GuiUtils.LabelSlider("Effect strength", effectSpeed, 0f, (float)ModSettings.I["strength_base"]);
			effectState = GuiUtils.LabelSlider("Effect state", effectState, 0f, 1f);
		}
	}
}
