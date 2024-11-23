﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firefly
{
	public class ModSettings
	{
		public enum ValueType
		{
			Boolean,
			Float
		}

		public class Field
		{
			public object value;
			public ValueType valueType;

			public Field(object value, ValueType valueType)
			{
				this.value = value;
				this.valueType = valueType;
			}
		}

		public Dictionary<string, Field> fields;

		public ModSettings(bool hdrOverride, bool useColliders, bool disableParticles, bool disableSparks, bool disableDebris, bool disableSmoke)
		{
			this.fields = new Dictionary<string, Field>()
			{
				{ "hdr_override", new Field(hdrOverride, ValueType.Boolean) },
				{ "use_colliders", new Field(useColliders, ValueType.Boolean) },
				{ "disable_particles", new Field(disableParticles, ValueType.Boolean) },
				{ "disable_sparks", new Field(disableSparks, ValueType.Boolean) },
				{ "disable_debris", new Field(disableDebris, ValueType.Boolean) },
				{ "disable_smoke", new Field(disableSmoke, ValueType.Boolean) }
			};
		}

		public ModSettings()
		{
			this.fields = new Dictionary<string, Field>();
		}

		public static ModSettings CreateDefault()
		{
			return new ModSettings(true, false, false, false, false, false);
		}

		/// <summary>
		/// Saves every field to a ConfigNode
		/// </summary>
		public void SaveToNode(ref ConfigNode node)
		{
			for (int i = 0; i < fields.Count; i++)
			{
				KeyValuePair<string, Field> elem = fields.ElementAt(i);
				node.AddValue(elem.Key, elem.Value.value);

				Logging.Log($"ModSettings -  Saved {elem.Key} to node as {elem.Value.value}");
			}
		}

		/// <summary>
		/// Gets the type of a field value
		/// </summary>
		public ValueType? GetFieldType(string key)
		{
			if (fields.ContainsKey(key))
			{
				return fields[key].valueType;
			}

			return null;
		}

		/// <summary>
		/// Gets a field from the dict specified by a key
		/// </summary>
		public Field GetField(string key)
		{
			if (fields.ContainsKey(key))
			{
				return fields[key];
			}

			return null;
		}
		
		// custom indexer
		public object this[string i]
		{
			get => fields[i].value;
			set => fields[i].value = value;
		}
	}

	public enum ModifierOperation
	{
		ADD = 0,
		SUBTRACT = 1,
		MULTIPLY = 2,
		DIVIDE = 3
	}

	public struct BodyColors
	{
		public Color glow;
		public Color glowHot;

		public Color trailPrimary;
		public Color trailSecondary;
		public Color trailTertiary;

		public Color wrapLayer;
		public Color wrapStreak;

		public Color shockwave;
	}

	public class BodyConfig
	{
		public string bodyName = "Unknown";

		// The entry speed gets multiplied by this before getting sent to the shader
		public float strengthMultiplier = 1f;

		// The trail length gets multiplied by this
		public float lengthMultiplier = 1f;

		// The trail opacity gets multiplied by this
		public float opacityMultiplier = 1f;

		// The wrap layer's fresnel effect is modified by this
		public float wrapFresnelModifier = 1f;

		// The threshold in m/s for particles to appear
		public float particleThreshold = 1800f;

		// This gets added to the streak probability
		public float streakProbability = 0f;

		// This gets added to the streak threshold, which is 0.5 by default (range is 0-1, where 1 is 4000 m/s, default is 0.5)
		public float streakThreshold = 0f;

		// Colors
		public BodyColors colors = new BodyColors();
	}

	public class PlanetPackConfig
	{
		// The speed gets multiplied by this after applying body configs
		public float speedMultiplier = 1f;

		// Affected bodies
		public string[] affectedBodies;
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class ConfigManager : MonoBehaviour
	{
		public static ConfigManager Instance { get; private set; }
		public const string SettingsPath = "GameData/Firefly/Configs/ModSettings.cfg";

		public ModSettings modSettings = ModSettings.CreateDefault();

		public Dictionary<string, BodyConfig> bodyConfigs = new Dictionary<string, BodyConfig>();
		public List<PlanetPackConfig> planetPackConfigs = new List<PlanetPackConfig>();

		public BodyConfig defaultConfig;

		public string homeWorld;

		public void Awake()
		{
			Instance = this;

			Logging.Log("ConfigManager Awake");
		}

		/// <summary>
		/// Method which gets ran after MM finishes patching, to allow for config patches
		/// </summary>
		public static void ModuleManagerPostLoad()
		{
			Logging.Log("ConfigManager MMPostLoad");

			Instance.StartLoading();
		}

		/// <summary>
		/// Loads every planet pack and body config
		/// </summary>
		public void StartLoading()
		{
			LoadModSettings();
			LoadPlanetConfigs();
		}
		
		/// <summary>
		/// Saves the mod setting overrides
		/// </summary>
		public void SaveModSettings()
		{
			Logging.Log("Saving mod settings");

			// create a parent node
			ConfigNode parent = new ConfigNode("ATMOFX_SETTINGS");

			// create the node
			ConfigNode node = new ConfigNode("ATMOFX_SETTINGS");

			modSettings.SaveToNode(ref node);

			// add to parent and save
			parent.AddNode(node);
			parent.Save(KSPUtil.ApplicationRootPath + SettingsPath);
		}

		/// <summary>
		/// Loads the mod settings
		/// </summary>
		void LoadModSettings()
		{
			// load settings
			ConfigNode[] settingsNodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_SETTINGS");
			modSettings = ModSettings.CreateDefault();

			if (settingsNodes.Length < 1)
			{
				// we don't have any saved settings or the user deleted the cfg file
				Logging.Log("Using default mod settings");
				return;
			}

			ConfigNode settingsNode = settingsNodes[0];

			// load the actual stuff from the ConfigNode
			bool isFormatted = true;
			modSettings["hdr_override"] = ReadSettingsField(settingsNode, "hdr_override", ref isFormatted);
			modSettings["use_colliders"] = ReadSettingsField(settingsNode, "use_colliders", ref isFormatted);
			modSettings["disable_particles"] = ReadSettingsField(settingsNode, "disable_particles", ref isFormatted);
			modSettings["disable_sparks"] = ReadSettingsField(settingsNode, "disable_sparks", ref isFormatted);
			modSettings["disable_debris"] = ReadSettingsField(settingsNode, "disable_debris", ref isFormatted);
			modSettings["disable_smoke"] = ReadSettingsField(settingsNode, "disable_smoke", ref isFormatted);

			if (!isFormatted)
			{
				Logging.Log("Settings cfg formatted incorrectly");
				modSettings = ModSettings.CreateDefault();
			}

			Logging.Log($"UseColliders:{modSettings["use_colliders"]} DisableParticles:{modSettings["disable_particles"]}");
		}

		/// <summary>
		/// Loads the planetpack and body configs
		/// </summary>
		void LoadPlanetConfigs()
		{
			// clear the dict and list
			bodyConfigs.Clear();
			planetPackConfigs.Clear();

			// get the planet packs
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PLANET_PACK");

			// check if there's actually anything to load
			if (nodes.Length > 0)
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					ProcessPlanetPackNode(nodes[i], out PlanetPackConfig cfg);

					planetPackConfigs.Add(cfg);
				}
			}

			// get the nodes
			nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_BODY");

			// check if there's actually anything to load
			if (nodes.Length > 0)
			{
				// iterate over every node and store the data
				for (int i = 0; i < nodes.Length; i++)
				{
					bool success = ProcessSingleNode(nodes[i], out BodyConfig body);

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

				strengthMultiplier = ReadConfigValue(node, "strength_multiplier", ref isFormatted),
				lengthMultiplier = ReadConfigValue(node, "length_multiplier", ref isFormatted),
				opacityMultiplier = ReadConfigValue(node, "opacity_multiplier", ref isFormatted),
				wrapFresnelModifier = ReadConfigValue(node, "wrap_fresnel_modifier", ref isFormatted),
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

			// apply planet pack configs
			for (int i = 0; i < planetPackConfigs.Count; i++)
			{
				// check if the body should be affected
				if (planetPackConfigs[i].affectedBodies.Contains(bodyName))
				{
					body.strengthMultiplier *= planetPackConfigs[i].speedMultiplier;
				}
			}

			return true;
		}

		/// <summary>
		/// Processes planet pack nodes
		/// </summary>
		bool ProcessPlanetPackNode(ConfigNode node, out PlanetPackConfig cfg)
		{
			Logging.Log($"Loading planet pack config");

			// create the config
			bool isFormatted = true;
			cfg = new PlanetPackConfig
			{
				speedMultiplier = ReadConfigValue(node, "speed_multiplier", ref isFormatted),
			};

			// read the affected body array
			ConfigNode affectedBodies = node.GetNode("AffectedBodies");
			string[] array = affectedBodies.GetValues("item");

			if (array.Length < 1)
			{
				Logging.Log("Planet pack config has zero affected bodies, it will not have any effect");
				return false;
			}

			cfg.affectedBodies = array;

			// is the config formatted correctly?
			if (!isFormatted)
			{
				Logging.Log($"Planet pack config is not formatted correctly: {node.name}");
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

			body.glow = ReadConfigColorHDR(colorNode, "glow", ref isFormatted);
			body.glowHot = ReadConfigColorHDR(colorNode, "glow_hot", ref isFormatted);
			body.trailPrimary = ReadConfigColorHDR(colorNode, "trail_primary", ref isFormatted);
			body.trailSecondary = ReadConfigColorHDR(colorNode, "trail_secondary", ref isFormatted);
			body.trailTertiary = ReadConfigColorHDR(colorNode, "trail_tertiary", ref isFormatted);
			body.wrapLayer = ReadConfigColorHDR(colorNode, "wrap_layer", ref isFormatted);
			body.wrapStreak = ReadConfigColorHDR(colorNode, "wrap_streak", ref isFormatted);
			body.shockwave = ReadConfigColorHDR(colorNode, "shockwave", ref isFormatted);

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
		/// Reads one boolean value from a node
		/// </summary>
		bool ReadConfigBoolean(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateBool(node.GetValue(key), out bool result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads one HDR color value from a node
		/// </summary>
		Color ReadConfigColorHDR(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Utils.EvaluateColorHDR(node.GetValue(key), out Color result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads the transition modifier mode enum
		/// </summary>
		ModifierOperation ReadConfigModifierOperation(ConfigNode node, string key, ref bool isFormatted)
		{
			bool success = Enum.TryParse(node.GetValue(key), out ModifierOperation result);
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Reads one boolean value from a node
		/// </summary>
		object ReadSettingsField(ConfigNode node, string field, ref bool isFormatted)
		{
			string value = node.GetValue(field);
			ModSettings.ValueType? type = modSettings.GetFieldType(field);

			if (value == null)
			{
				isFormatted = false;
				return null;
			}

			bool success = false;
			object result = default;
			switch (type)
			{
				case ModSettings.ValueType.Boolean:
					bool result_bool;
					success = Utils.EvaluateBool(value, out result_bool);
					result = result_bool;
					break;
				case ModSettings.ValueType.Float:
					float result_float;
					success = Utils.EvaluateFloat(value, out result_float);
					result = result_float;
					break;
				default: break;
			}
			
			isFormatted = isFormatted && success;

			return result;
		}

		/// <summary>
		/// Tries getting the body config for a specified body name, and fallbacks if desired
		/// </summary>
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
