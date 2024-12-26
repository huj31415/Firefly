using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Firefly
{
	internal class GuiUtils
	{
		/// <summary>
		/// Draws a config field with a toggle switch
		/// </summary>
		/// <param name="label">Label to show</param>
		/// <param name="tgl">The dict contatining the toggle values</param>
		/// <returns>The apply button state</returns>
		public static void DrawConfigFieldBool(string label, Dictionary<string, ModSettings.Field> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			tgl[label].value = GUILayout.Toggle((bool)tgl[label].value, label + needsReload);
		}

		/// <summary>
		/// Draws a config field with a number input
		/// </summary>
		/// <param name="label">Label to show</param>
		/// <param name="tgl">The dict contatining the toggle values</param>
		/// <returns>The apply button state</returns>
		public static void DrawConfigFieldFloat(string label, Dictionary<string, ModSettings.Field> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			GUILayout.BeginHorizontal();
			GUILayout.Label(label + needsReload);

			string text = GUILayout.TextField(((float)tgl[label].value).ToString());
			bool hasValue = float.TryParse(text, out float value);
			if (hasValue) tgl[label].value = value;

			GUILayout.EndHorizontal();
		}

		public static float LabelSlider(string label, float value, float startValue, float endValue)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);

			float v = GUILayout.HorizontalSlider(value, startValue, endValue);

			GUILayout.EndHorizontal();

			return v;
		}

		public static void DrawFloatInput(string label, ref float value)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(label);

			string text = GUILayout.TextField(((float)value).ToString());
			bool hasValue = float.TryParse(text, out float v);
			if (hasValue) value = v;

			GUILayout.EndHorizontal();
		}
	}
}
