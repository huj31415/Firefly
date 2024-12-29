using UnityEngine;

namespace Firefly
{
	public static class AtmoFxLayers
	{
		public const int Spacecraft = 0;
		public const int Fx = 23;
	}

	public class Logging
	{
		public const string Prefix = "[Firefly] ";

		public static void Log(object message)
		{
			Debug.Log(Prefix + message);
		}
	}

	public static class Utils
	{
		public static bool EvaluateFloat(string text, out float val)
		{
			return float.TryParse(text, out val);
		}

		public static bool EvaluateBool(string text, out bool val)
		{
			return bool.TryParse(text.ToLower(), out val);
		}

		public static bool EvaluateColorHDR(string text, out Color val)
		{
			bool isFormatted = true;
			val = Color.magenta;

			string[] channels = text.Split(' ');
			if (channels.Length < 4) return false;

			// evaluate the values
			float r = 0f;
			float g = 0f;
			float b = 0f;
			float i = 0f;
			isFormatted = isFormatted && EvaluateFloat(channels[0], out r);
			isFormatted = isFormatted && EvaluateFloat(channels[1], out g);
			isFormatted = isFormatted && EvaluateFloat(channels[2], out b);
			isFormatted = isFormatted && EvaluateFloat(channels[3], out i);

			// divide by 255 to convert into 0-1 range
			r /= 255f;
			g /= 255f;
			b /= 255f;

			float factor = Mathf.Pow(2f, i);
			val = new Color(r * factor, g * factor, b * factor);

			return isFormatted;
		}

		/// <summary>
		/// Returns the cfg name from a part.partInfo.name field
		/// </summary>
		public static string GetPartCfgName(string name)
		{
			return name.Replace('.', '_');
		}

		/// <summary>
		/// Is part legible for bound calculations?
		/// </summary>
		public static bool IsPartBoundCompatible(Part part)
		{
			return IsPartCompatible(part) && !(
				part.Modules.Contains("ModuleParachute")
			);
		}

		/// <summary>
		/// Is part legible for fx envelope calculations?
		/// </summary>
		public static bool IsPartCompatible(Part part)
		{
			return !(
				part.Modules.Contains("ModuleConformalDecal") ||
				part.Modules.Contains("ModuleConformalFlag") ||
				part.Modules.Contains("ModuleConformalText")
			);
		}

		/// <summary>
		/// Landing gear have flare meshes for some reason, this function checks if a mesh is a flare or not
		/// </summary>
		public static bool CheckWheelFlareModel(Part part, string model)
		{
			bool isFlare = string.Equals(model, "flare", System.StringComparison.OrdinalIgnoreCase);
			bool isWheel = part.HasModuleImplementing<ModuleWheelBase>();

			return isFlare && isWheel;
		}

		/// <summary>
		/// Check if a model's layer is incorrect
		/// </summary>
		public static bool CheckLayerModel(Transform model)
		{
			return (
				model.gameObject.layer == 1
			);
		}

		/// <summary>
		/// Returns the angle of attack
		/// Code courtesy FAR
		/// </summary>
		public static float GetAngleOfAttack(Vessel vessel)
		{
			// Code courtesy FAR.
			Transform refTransform = vessel.GetTransform();
			Vector3 velVectorNorm = vessel.srf_velocity.normalized;

			Vector3 tmpVec = refTransform.up * Vector3.Dot(refTransform.up, velVectorNorm) + refTransform.forward * Vector3.Dot(refTransform.forward, velVectorNorm);   //velocity vector projected onto a plane that divides the airplane into left and right halves
			float AoA = Vector3.Dot(tmpVec.normalized, refTransform.forward);
			AoA = Mathf.Rad2Deg * Mathf.Asin(AoA);
			if (float.IsNaN(AoA))
			{
				AoA = 0.0f;
			}

			return AoA;
		}

		/// <summary>
		/// Returns the corners of a given Bounds object
		/// </summary>
		public static Vector3[] GetBoundCorners(Bounds bounds)
		{
			Vector3 center = bounds.center;
			float x = bounds.extents.x;
			float y = bounds.extents.y;
			float z = bounds.extents.z;

			Vector3[] corners = new Vector3[8];

			corners[0] = center + new Vector3(x, y, z);
			corners[1] = center + new Vector3(x, y, -z);
			corners[2] = center + new Vector3(-x, y, z);
			corners[3] = center + new Vector3(-x, y, -z);

			corners[4] = center + new Vector3(x, -y, z);
			corners[5] = center + new Vector3(x, -y, -z);
			corners[6] = center + new Vector3(-x, -y, z);
			corners[7] = center + new Vector3(-x, -y, -z);

			return corners;
		}

		/// <summary>
		/// Returns a Color from HSV values
		/// </summary>
		public static Color ColorHSV(float h, float s, float v)
		{
			return Color.HSVToRGB(h, s, v);
		}

		/// <summary>
		/// Returns HSV from a color
		/// </summary>
		public static void ColorHSV(Color c, out float h, out float s, out float v)
		{
			Color.RGBToHSV(c, out h, out s, out v);
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

		public static Texture2D GenerateHueTexture(int width, int height, float s, float v)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false, false);

			Color c;
			for (int x = 0; x < width; x++)
			{
				c = Utils.ColorHSV((float)x / (float)width, s, v);
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
