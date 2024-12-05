using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Firefly
{
	public class ModSettings
	{
		public static ModSettings Instance { get; private set; }

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

		public ModSettings()
		{
			this.fields = new Dictionary<string, Field>();

			Instance = this;
		}

		public static ModSettings CreateDefault()
		{
			ModSettings ms = new ModSettings
			{
				fields = new Dictionary<string, Field>()
				{
					{ "hdr_override", new Field(true, ValueType.Boolean) },
					{ "use_colliders", new Field(false, ValueType.Boolean) },
					{ "disable_particles", new Field(false, ValueType.Boolean) },
					{ "disable_sparks", new Field(false, ValueType.Boolean) },
					{ "disable_debris", new Field(false, ValueType.Boolean) },
					{ "disable_smoke", new Field(false, ValueType.Boolean) },
					{ "strength_base", new Field(2800f, ValueType.Float) }
				}
			};

			return ms;
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

	public class BodyColors
	{
		public Color? glow;
		public Color? glowHot;

		public Color? trailPrimary;
		public Color? trailSecondary;
		public Color? trailTertiary;
		public Color? trailStreak;

		public Color? wrapLayer;
		public Color? wrapStreak;

		public Color? shockwave;

		public BodyColors()
		{

		}

		/// <summary>
		/// Creates a copy of another BodyColors
		/// </summary>
		public BodyColors(BodyColors org)
		{
			this.glow = org.glow;
			this.glowHot = org.glowHot;

			this.trailPrimary = org.trailPrimary;
			this.trailSecondary = org.trailSecondary;
			this.trailTertiary = org.trailTertiary;
			this.trailStreak = org.trailStreak;

			this.wrapLayer = org.wrapLayer;
			this.wrapStreak = org.wrapStreak;

			this.shockwave = org.shockwave;
		}
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

		public Dictionary<string, BodyColors> partConfigs = new Dictionary<string, BodyColors>();

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
			LoadPartConfigs();
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
			for (int i = 0; i < modSettings.fields.Count; i++)
			{
				KeyValuePair<string, ModSettings.Field> e = modSettings.fields.ElementAt(i);

				modSettings[e.Key] = ReadSettingsField(settingsNode, e.Key, ref isFormatted);
			}

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
		/// Loads the configs for individual parts
		/// </summary>
		void LoadPartConfigs()
		{
			// clear the dict
			partConfigs.Clear();

			// get the planet packs
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_PART");

			// check if there's actually anything to load
			if (nodes.Length > 0)
			{
				for (int i = 0; i < nodes.Length; i++)
				{
					string partId = nodes[i].GetValue("name");
					bool success = ProcessPartConfigNode(nodes[i], out BodyColors cfg);

					Logging.Log($"Processed part override config {partId}");

					if (!success)
					{
						Logging.Log($"Couldn't process override config for part {partId}");
						continue;
					}

					partConfigs.Add(partId, cfg);
				}
			}
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
			isFormatted = isFormatted && ProcessBodyColors(node, false, out body.colors);

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
			string array = node.GetValue("affected_bodies");

			string[] strings = array.Split(',');
			for (int i = 0; i < strings.Length; i++)
			{
				strings[i] = strings[i].Trim();
			}

			if (strings.Length < 1)
			{
				Logging.Log("Planet pack config has zero affected bodies, it will not have any effect");
				return false;
			}

			cfg.affectedBodies = strings;

			// is the config formatted correctly?
			if (!isFormatted)
			{
				Logging.Log($"Planet pack config is not formatted correctly: {node.name}");
				return false;
			}

			return true;
		}

		/// <summary>
		/// Processes a single part config node
		/// </summary>
		bool ProcessPartConfigNode(ConfigNode node, out BodyColors cfg)
		{
			bool isFormatted = ProcessBodyColors(node, true, out cfg);

			return isFormatted;
		}

		/// <summary>
		/// Processes the colors node of a body
		/// </summary>
		bool ProcessBodyColors(ConfigNode rootNode, bool partConfig, out BodyColors body)
		{
			body = new BodyColors();

			ConfigNode colorNode = new ConfigNode();
			bool isFormatted = rootNode.TryGetNode("Color", ref colorNode);
			if (!isFormatted) return false;

			body.glow = ReadConfigColorHDR(colorNode, "glow", partConfig, ref isFormatted);
			body.glowHot = ReadConfigColorHDR(colorNode, "glow_hot", partConfig, ref isFormatted);
			body.trailPrimary = ReadConfigColorHDR(colorNode, "trail_primary", partConfig, ref isFormatted);
			body.trailSecondary = ReadConfigColorHDR(colorNode, "trail_secondary", partConfig, ref isFormatted);
			body.trailTertiary = ReadConfigColorHDR(colorNode, "trail_tertiary", partConfig, ref isFormatted);
			body.trailStreak = ReadConfigColorHDR(colorNode, "trail_streak", partConfig, ref isFormatted);
			body.wrapLayer = ReadConfigColorHDR(colorNode, "wrap_layer", partConfig, ref isFormatted);
			body.wrapStreak = ReadConfigColorHDR(colorNode, "wrap_streak", partConfig, ref isFormatted);
			body.shockwave = ReadConfigColorHDR(colorNode, "shockwave", partConfig, ref isFormatted);

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
		Color? ReadConfigColorHDR(ConfigNode node, string key, bool partConfig, ref bool isFormatted)
		{
			// check if exists
			if (!node.HasValue(key))
			{
				isFormatted = isFormatted && partConfig;

				return null;
			}

			// get the value
			string value = node.GetValue(key);

			// check if null
			if (value.ToLower() == "null" || value.ToLower() == "default")
			{
				isFormatted = isFormatted && partConfig;

				return null;
			}

			bool success = Utils.EvaluateColorHDR(value, out Color result);
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
