using Steamworks;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

namespace Firefly
{
	internal class ColorPickerWindow
	{
		const int pickerSize = 300;
		const int sliderSize = 220;

		public Rect windowRect;
		public string title;
		public bool show;

		float h, s, v;
		Color color;

		bool rgbMode;
		float[] raw = new float[3];
		string[] ui_raw = new string[3];
		Texture2D[] sliderTex = new Texture2D[3];
		Rect[] sliderRects = new Rect[3];

		Rect hueBarRect;
		Rect pickerRect;
		Texture2D pickerTex;
		Texture2D hueTex;
		Texture2D colorTex;
		Texture2D hueSelectorTex;
		Texture2D pickerSelectorTex;

		bool isPicking;
		float pickTimer;

		public ColorPickerWindow(string title, float x, float y, Color c)
		{
			windowRect = new Rect(x, y, 300f, 100f);
			this.title = title;

			color = c;
			Utils.ColorHSV(c, out h, out s, out v);

			hueTex = TextureUtils.GenerateHueTexture(120, 20);
			pickerTex = TextureUtils.GenerateGradientTexture(pickerSize, pickerSize, h);
			colorTex = TextureUtils.GenerateColorTexture(1, 1, color);

			hueSelectorTex = TextureUtils.GenerateSelectorTexture(3, 24, 1, Color.white, Color.black);
			pickerSelectorTex = TextureUtils.GenerateSelectorTexture(3, 3, 1, Color.white, Color.black);

			sliderTex[0] = TextureUtils.GenerateHueTexture(120, 20);
			sliderTex[1] = TextureUtils.GenerateGradientTexture(100, 20, Utils.ColorHSV(h, 0f, v), Utils.ColorHSV(h, 1f, v));
			sliderTex[2] = TextureUtils.GenerateGradientTexture(100, 20, Utils.ColorHSV(h, s, 0f), Utils.ColorHSV(h, s, 1f));
		}

		public void Gui()
		{
			if (!show) return;

			windowRect = GUILayout.Window(511, windowRect, Window, title);
		}

		void Window(int id)
		{
			GUILayout.BeginVertical();

			DrawColor();
			GUILayout.Space(20);

			DrawHueBar();
			GUILayout.Space(10);

			DrawPicker();
			GUILayout.Space(20);

			DrawSliders();
			GUILayout.Space(20);

			DrawCloseButton();

			GUILayout.EndVertical();

			HandleInput();
		}

		void DrawCloseButton()
		{
			GUILayout.Button("Save and close");
		}

		void DrawColor()
		{
			Rect rect = GUILayoutUtility.GetRect(120f, 20f, GUILayout.Width(pickerSize));
			GUI.DrawTexture(rect, colorTex);
		}

		void DrawHueBar()
		{
			Rect rect = GUILayoutUtility.GetRect(120f, 20f, GUILayout.Width(pickerSize));
			GUI.DrawTexture(rect, hueTex);
			hueBarRect = GUIUtility.GUIToScreenRect(rect);

			// selector
			rect = new Rect(rect.x + h * rect.width, rect.y - 1, 3, 22);
			GUI.DrawTexture(rect, hueSelectorTex);
		}

		void DrawPicker()
		{
			Rect rect = GUILayoutUtility.GetRect(120f, 120f, GUILayout.Width(pickerSize), GUILayout.Height(pickerSize));
			GUI.DrawTexture(rect, pickerTex);
			pickerRect = GUIUtility.GUIToScreenRect(rect);

			// selector
			rect = new Rect(rect.x + s * rect.width, rect.y + (1f - v) * rect.height, 3, 3);
			GUI.DrawTexture(rect, pickerSelectorTex);
		}

		void DrawSliders()
		{
			DrawColorSlider("H", 0);
			DrawColorSlider("S", 1);
			DrawColorSlider("V", 2);
		}

		void DrawColorSlider(string label, int index)
		{
			GUILayout.BeginHorizontal();

			// label
			GUILayout.Label(label, GUILayout.Width(40));

			// slider
			Rect rect = GUILayoutUtility.GetRect(120f, 20f, GUILayout.Width(sliderSize));
			GUI.DrawTexture(rect, sliderTex[index]);
			sliderRects[index] = GUIUtility.GUIToScreenRect(rect);

			// selector
			rect = new Rect(rect.x + raw[index] * rect.width, rect.y - 1, 5, 22);
			GUI.DrawTexture(rect, hueSelectorTex);

			// number input
			string newText = GUILayout.TextField(ui_raw[index], GUILayout.Width(40));
			bool hasValue = float.TryParse(newText, out float v);
			if (newText != ui_raw[index] && hasValue)  // only set the value if it changed and if it's a correct float
			{
				float clamped = Mathf.Clamp(v, 0f, 255f);
				ui_raw[index] = $"{clamped:F0}";  // display the value as an int
				raw[index] = clamped / 255f;

				OnSliderChange();
			}

			GUILayout.EndHorizontal();
		}

		void HandleHueInput(Vector2 mouse)
		{
			bool inBar = GuiUtils.GetRectPoint(mouse, hueBarRect, out Vector2 clickPoint);

			if (inBar)
			{
				isPicking = true;

				h = clickPoint.x / hueBarRect.width;

				// regenerate picker texture
				pickerTex = TextureUtils.GenerateGradientTexture(pickerSize, pickerSize, h);
				UpdateColor();
			}
		}

		void HandlePickerInput(Vector2 mouse)
		{
			bool inBar = GuiUtils.GetRectPoint(mouse, pickerRect, out Vector2 clickPoint);

			if (inBar)
			{
				isPicking = true;

				Color c = pickerTex.GetPixel((int)clickPoint.x, (int)clickPoint.y);
				Utils.ColorHSV(c, out _, out s, out v);

				UpdateColor();
			}
		}

		void HandleSliderInput(Vector2 mouse)
		{
			for (int i = 0; i < 3; i++)
			{
				bool inBar = GuiUtils.GetRectPoint(mouse, sliderRects[i], out Vector2 clickPoint);
				if (!inBar) continue;

				isPicking = true;

				raw[i] = clickPoint.x / sliderRects[i].width;

				OnSliderChange();
			}
		}

		void HandleInput()
		{
			// handle clicks
			if (Input.GetMouseButton(0) && Event.current.type == EventType.Repaint)
			{
				Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

				HandleHueInput(mouse);
				HandlePickerInput(mouse);
				HandleSliderInput(mouse);
			}

			// handle unclicks
			if (Input.GetMouseButtonUp(0) && isPicking)
			{
				isPicking = false;
				pickTimer = 0.5f;
			}

			if (pickTimer > 0f) pickTimer -= Time.deltaTime;

			// handle drag
			if (!isPicking && pickTimer <= 0f) GUI.DragWindow();
		}

		void UpdateColor()
		{
			color = Utils.ColorHSV(h, s, v);
			colorTex = TextureUtils.GenerateColorTexture(1, 1, color);

			// update slider textures
			sliderTex[0] = TextureUtils.GenerateHueTexture(120, 20);
			sliderTex[1] = TextureUtils.GenerateGradientTexture(100, 20, Utils.ColorHSV(h, 0f, v), Utils.ColorHSV(h, 1f, v));
			sliderTex[2] = TextureUtils.GenerateGradientTexture(100, 20, Utils.ColorHSV(h, s, 0f), Utils.ColorHSV(h, s, 1f));

			// update sliders
			raw[0] = h;
			raw[1] = s;
			raw[2] = v;
			
			// update text fields
			ui_raw[0] = $"{(h * 255f):F0}";
			ui_raw[1] = $"{(s * 255f):F0}";
			ui_raw[2] = $"{(v * 255f):F0}";
		}

		void OnSliderChange()
		{
			// update color
			h = raw[0];
			s = raw[1];
			v = raw[2];

			pickerTex = TextureUtils.GenerateGradientTexture(pickerSize, pickerSize, h);
			UpdateColor();
		}
	}

	internal class TextureUtils
	{
		public static Texture2D GenerateHueTexture(int width, int height)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				c = Utils.ColorHSV((float)x / (float)width, 1f, 1f);
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateGradientTexture(int width, int height, Color c1, Color c2)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				c = Color.Lerp(c1, c2, (float)x / (float)width);
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateGradientTexture(int width, int height, float hue)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					c = Utils.ColorHSV(hue, (float)x / (float)width, (float)y / (float)height);
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateColorTexture(int width, int height, Color c)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}

		public static Texture2D GenerateSelectorTexture(int width, int height, int border, Color insideColor, Color color)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					if (x < border || x > width - 1 - border) c = color;
					else if (y < border || y > height - 1 - border) c = color;
					else c = insideColor;

					tex.SetPixel(x, y, c);
				}
			}

			tex.Apply();
			return tex;
		}
	}
}
