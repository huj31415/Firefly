using System.Collections.Generic;
using UnityEngine;

namespace Firefly
{
	internal class EffectEditor
	{
		public static EffectEditor Instance { get; private set; }

		public Vector3 effectDirection;
		public float effectSpeed;
		public float effectState;

		string currentBody;
		BodyConfig config;

		AtmoFxModule fxModule = null;

		void ApplyConfig()
		{
			ConfigManager.Instance.bodyConfigs[currentBody] = config;
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

		public void Gui()
		{
			GUILayout.BeginVertical();

			effectSpeed = GuiUtils.LabelSlider("Effect strength", effectSpeed, 0f, (float)ModSettings.I["strength_base"]);
			effectState = GuiUtils.LabelSlider("Effect state", effectState, 0f, 1f);

			if (GUILayout.Button("Align effects to camera")) ApplyCameraDirection();

			// end
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		public void Close()
		{
			fxModule.doEffectEditor = false;
		}
	}
}
