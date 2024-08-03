using System.Collections.Generic;
using UnityEngine;

namespace AtmosphericFx
{
	public struct BodyColors
	{
		public Color glow;
		public Color glowHot;

		public Color trailPrimary;
		public Color trailSecondary;
		public Color trailTertiary;

		public Color shockwave;
	}

	public class BodyConfig
	{
		public string bodyName = "Unknown";

		// The entry speed gets multiplied by this before getting sent to the shader
		public float intensity = 1f;

		// This gets added to the AeroFX scalar value
		public float transitionScalar = 0f;

		// The trail length gets multiplied by this
		public float lengthMultiplier = 1f;

		// The threshold in m/s for particles to appear
		public float particleThreshold = 1800f;

		// This gets added to the streak probability
		public float streakProbability = 0f;

		// This gets added to the streak threshold, which is 0.5 by default (range is 0-1, where 1 is 4000 m/s, default is 0.5)
		public float streakThreshold = 0f;

		// Colors
		public BodyColors colors = new BodyColors();
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class ConfigManager : MonoBehaviour
	{
		public static ConfigManager Instance { get; private set; }

		public Dictionary<string, BodyConfig> bodyConfigs = new Dictionary<string, BodyConfig>();

		public BodyConfig defaultConfig;

		public string homeWorld;

		public void Awake()
		{
			Instance = this;

			Logging.Log("ConfigManager Awake");
		}

		public static void ModuleManagerPostLoad()
		{
			Logging.Log("ConfigManager MMPostLoad");

			Instance.StartLoading();
		}

		/// <summary>
		/// Loads every config
		/// </summary>
		public void StartLoading()
		{
			// clear the dict
			bodyConfigs.Clear();

			// get the nodes
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_BODY");

			// check if there's actually anything to load
			if (nodes.Length > 0)
			{
				// iterate over every node and store the data
				foreach (ConfigNode node in nodes)
				{
					bool success = ProcessSingleNode(node, out BodyConfig body);

					// couldn't load the config
					if (!success)
					{
						Logging.Log("Body couldn't be loaded");
						continue;
					}

					bodyConfigs.Add(body.bodyName, body);
				}
			}

			// get the default config
			bool hasDefault = bodyConfigs.ContainsKey("Default");
			if (!hasDefault)
			{
				// some nice error message, since this is a pretty bad one
				Logging.Log("-------------------------------------------");
				Logging.Log("Default config not loaded, halting startup.");
				Logging.Log("This likely means a corrupted install.");
				Logging.Log("-------------------------------------------");

				return;
			}

			defaultConfig = bodyConfigs["Default"];
		}

		/// <summary>
		/// Processes single node
		/// </summary>
		bool ProcessSingleNode(ConfigNode node, out BodyConfig body)
		{
			body = null;

			string bodyName = node.GetValue("name");

			Logging.Log($"Loading body {bodyName}");

			// make sure there aren't any duplicates
			if (bodyConfigs.ContainsKey(bodyName))
			{
				Logging.Log($"Duplicate body config found: {bodyName}");
				return false;
			}

			// create the config
			bool isFormatted = true;
			body = new BodyConfig
			{
				bodyName = bodyName,

				intensity = ReadConfigValue(node, "intensity", ref isFormatted),
				transitionScalar = ReadConfigValue(node, "transition_scalar", ref isFormatted),
				lengthMultiplier = ReadConfigValue(node, "length_multiplier", ref isFormatted),
				particleThreshold = ReadConfigValue(node, "particle_threshold", ref isFormatted),
				streakProbability = ReadConfigValue(node, "streak_probability", ref isFormatted),
				streakThreshold = ReadConfigValue(node, "streak_threshold", ref isFormatted)
			};

			// read the colors
			isFormatted = isFormatted && ProcessBodyColors(node, out body.colors);

			// is the config formatted correctly?
			if (!isFormatted)
			{
				Logging.Log($"Body config is not formatted correctly: {bodyName}");
				return false;
			}

			return true;
		}

		/// <summary>
		/// Processes the colors node of a body
		/// </summary>
		bool ProcessBodyColors(ConfigNode rootNode, out BodyColors body)
		{
			body = new BodyColors();

			ConfigNode colorNode = new ConfigNode();
			bool isFormatted = rootNode.TryGetNode("Color", ref colorNode);
			if (!isFormatted) return false;

			body.glow = ReadConfigValueHDR(colorNode, "glow", ref isFormatted);
			body.glowHot = ReadConfigValueHDR(colorNode, "glow_hot", ref isFormatted);
			body.trailPrimary = ReadConfigValueHDR(colorNode, "trail_primary", ref isFormatted);
			body.trailSecondary = ReadConfigValueHDR(colorNode, "trail_secondary", ref isFormatted);
			body.trailTertiary = ReadConfigValueHDR(colorNode, "trail_tertiary", ref isFormatted);
			body.shockwave = ReadConfigValueHDR(colorNode, "shockwave", ref isFormatted);

			return isFormatted;
		}

		/// <summary>
		/// Reads one float value from a node
		/// </summary>
		float ReadConfigValue(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateFloat(node.GetValue(key), out float result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads one HDR color value from a node
		/// </summary>
		Color ReadConfigValueHDR(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateColorHDR(node.GetValue(key), out Color result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Tries getting the body config for a specified body name, and fallbacks if desired
		/// </summary>
		/// <param name="bodyName">Name of the body</param>
		/// <param name="fallback">Should the function fallback?</param>
		/// <param name="cfg">The output config</param>
		/// <returns>Returns false if the function didn't find a loaded config for the specified name, true otherwise</returns>
		public bool TryGetBodyConfig(string bodyName, bool fallback, out BodyConfig cfg)
		{
			bool hasConfig = bodyConfigs.ContainsKey(bodyName);

			if (hasConfig)
			{
				cfg = bodyConfigs[bodyName];
			} else
			{
				// null the cfg, or fallback to the default one
				cfg = null;
				if (fallback) cfg = defaultConfig;
			}

			return hasConfig;
		}

		/// <summary>
		/// Gets the body config for a specified vessel
		/// </summary>
		public BodyConfig GetVesselBody(Vessel vessel)
		{
			TryGetBodyConfig(vessel.mainBody.bodyName, true, out BodyConfig cfg);
			return cfg;
		}
	}
}
