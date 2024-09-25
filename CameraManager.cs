using UnityEngine;

namespace AtmosphericFx
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class CameraManager : MonoBehaviour
	{
		public static CameraManager Instance { get; private set; }

		public bool isHdr = false;

		public void Awake()
		{
			Instance = this;
			
			OverrideHDR(ConfigManager.Instance.modSettings.hdrOverride);
		}

		public void OverrideHDR(bool hdr)
		{
			isHdr = hdr;

			ConfigManager.Instance.modSettings.hdrOverride = hdr;

			if (Camera.main != null)
			{
				Camera.main.allowHDR = hdr;
			}

			if (InternalCamera.Instance != null)
			{
				InternalCamera.Instance.GetComponent<Camera>().allowHDR = hdr;
			}
		}
	}
}
