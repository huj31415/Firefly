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

		float intensity;
		float h, s, v;
		Color color;

		// sliders
		bool rgbMode = false;
		float[] raw = new float[4];
		string[] ui_raw = new string[4];
		Texture2D[] sliderTex = new Texture2D[4];
		Rect[] sliderRects = new Rect[4];

		// selectors and textures
		Rect hueBarRect;
		Rect pickerRect;
		Texture2D pickerTex;
		Texture2D hueTex;
		Texture2D colorTex;
		Texture2D hueSelectorTex;
		Texture2D pickerSelectorTex;

		// pick state
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

			GenerateSliderTextures();
			sliderTex[3] = TextureUtils.GenerateGradientTexture(120, 20, Color.black, Color.white);
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
			DrawBottomControls();

			GUILayout.EndVertical();

			// after drawing handle input
			HandleInput();
		}

		void DrawBottomControls()
		{
			bool previousRgb = rgbMode;
			rgbMode = GUILayout.Toggle(rgbMode, "RGB mode");
			if (previousRgb != rgbMode) OnRGBToggle();

			GUILayout.Space(20);
			if (GUILayout.Button("Save and close")) show = false;
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
			DrawColorSlider(rgbMode ? "R" : "H", 0, true);
			DrawColorSlider(rgbMode ? "G" : "S", 1, true);
			DrawColorSlider(rgbMode ? "B" : "V", 2, true);
			DrawColorSlider("I", 3, false);  // HDR intensity
		}

		void DrawColorSlider(string label, int index, bool isColor)
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
				if (isColor)
				{
					float clamped = Mathf.Clamp(v, 0f, 255f);
					ui_raw[index] = $"{clamped:F0}";  // display the value as an int
					raw[index] = clamped / 255f;
				} else
				{
					// HDR intensity slider
					raw[index] = Mathf.Clamp(v, 0f, 5f);
					ui_raw[index] = $"{raw[index]:F4}";
				}
				
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
			for (int i = 0; i < 4; i++)
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

			// update sliders
			if (rgbMode)
			{
				raw[0] = color.r;
				raw[1] = color.g;
				raw[2] = color.b;
			}
			else
			{
				raw[0] = h;
				raw[1] = s;
				raw[2] = v;
			}

			// update slider textures
			GenerateSliderTextures();
			
			// update text fields
			ui_raw[0] = $"{(raw[0] * 255f):F0}";
			ui_raw[1] = $"{(raw[1] * 255f):F0}";
			ui_raw[2] = $"{(raw[2] * 255f):F0}";
		}

		void OnSliderChange()
		{
			// update color
			if (rgbMode)
			{
				Utils.ColorHSV(new Color(raw[0], raw[1], raw[2]), out h, out s, out v);
			} else
			{
				h = raw[0];
				s = raw[1];
				v = raw[2];
			}

			// update intensity
			intensity = raw[3];

			pickerTex = TextureUtils.GenerateGradientTexture(pickerSize, pickerSize, h);
			UpdateColor();
		}

		void OnRGBToggle()
		{
			if (rgbMode)
			{
				raw[0] = color.r;
				raw[1] = color.g;
				raw[2] = color.b;
			}
			else
			{
				raw[0] = h;
				raw[1] = s;
				raw[2] = v;
			}

			OnSliderChange();
		}
		
		void GenerateSliderTextures()
		{
			if (rgbMode)
			{
				sliderTex[0] = TextureUtils.GenerateGradientTexture(100, 20, new Color(0f, color.g, color.b), new Color(1f, color.g, color.b));
				sliderTex[1] = TextureUtils.GenerateGradientTexture(100, 20, new Color(color.r, 0f, color.b), new Color(color.r, 1f, color.b));
				sliderTex[2] = TextureUtils.GenerateGradientTexture(100, 20, new Color(color.r, color.g, 0f), new Color(color.r, color.g, 1f));
			} else
			{
				sliderTex[0] = TextureUtils.GenerateHueTexture(100, 20, s, v);
				sliderTex[1] = TextureUtils.GenerateGradientTexture(100, 20, Utils.ColorHSV(h, 0f, v), Utils.ColorHSV(h, 1f, v));
				sliderTex[2] = TextureUtils.GenerateGradientTexture(100, 20, Utils.ColorHSV(h, s, 0f), Utils.ColorHSV(h, s, 1f));
			}
		}
	}
}
