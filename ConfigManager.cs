using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtmosphericFx
{
	public class BodyConfig
	{
		
	}

	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class ConfigManager : MonoBehaviour
	{
		public static ConfigManager Instance { get; private set; }

		public Dictionary<string, BodyConfig> bodyConfigs = new Dictionary<string, BodyConfig>();

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

		public void StartLoading()
		{
			// get the nodes
			ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("ATMOFX_BODY");

			// check if there's actually anything to store
			if (nodes.Length > 0)
			{
				// iterate over every node and store the data
				foreach (ConfigNode node in nodes)
				{
					string bodyName = node.GetValue("bodyName");

					Logging.Log($"Loaded body {bodyName}");

					// make sure there aren't any duplicates
					if (bodyConfigs.ContainsKey(bodyName))
					{
						Logging.Log($"Duplicate body config found: {bodyName}");
					} else
					{
						BodyConfig body = new BodyConfig();

						bodyConfigs.Add(bodyName, body);
					}
				}
			}
		}
	}
}
