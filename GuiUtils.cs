using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static ProceduralSpaceObject;

namespace Firefly
{
	internal class GuiUtils
	{
		public static void DrawConfigFieldBool(string label, Dictionary<string, ModSettings.Field> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			tgl[label].value = GUILayout.Toggle((bool)tgl[label].value, label + needsReload);
		}

		public static void DrawConfigFieldFloat(string label, Dictionary<string, ModSettings.Field> tgl)
		{
			string needsReload = tgl[label].needsReload ? "*" : "";

			GUILayout.BeginHorizontal();
			GUILayout.Label(label + needsReload);

			tgl[label].uiText = GUILayout.TextField(tgl[label].uiText);
			bool hasValue = float.TryParse(tgl[label].uiText, out float value);
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

		public static void DrawFloatInput(string label, ref string text, ref float value, params GUILayoutOption[] layoutOptions)
		{
			GUILayout.BeginHorizontal(layoutOptions);
			GUILayout.Label(label);

			text = GUILayout.TextField(text);
			bool hasValue = float.TryParse(text, out float v);
			if (hasValue) value = v;

			GUILayout.EndHorizontal();
		}

		public static bool GetRectPoint(Vector2 point, Rect rect, out Vector2 result)
		{
			if (rect.Contains(point))
			{
				result = new Vector2(
					Mathf.Clamp(point.x - rect.xMin, 0f, rect.width),
					rect.width - Mathf.Clamp(point.y - rect.yMin, 0f, rect.height)
				);

				return true;
			}

			result = Vector2.zero;
			return false;
		}

		public static bool DrawColorButton(string label, Texture2D pix, Color color)
		{
			GUILayout.BeginHorizontal();

			GUILayout.Label(label);

			bool b = GUILayout.Button("", GUILayout.Width(60), GUILayout.Height(20));
			Rect rect = GUILayoutUtility.GetLastRect();
			rect = new Rect(rect.x + 4, rect.y + 4, rect.width - 8, rect.height - 8);
			GUI.DrawTexture(rect, pix, ScaleMode.StretchToFill, false, 0f, color, 0f, 0f);

			GUILayout.EndHorizontal();

			return b;
		}
	}
}
