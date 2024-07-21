using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AtmosphericFx
{
	public static class AtmoFxLayers
	{
		public const int Spacecraft = 0;
		public const int Fx = 15;
	}

	public class Logging
	{
		public const string Prefix = "[AtmosphericFx] ";

		public static void Log(object message)
		{
			Debug.Log(Prefix + message);
		}
	}
}
