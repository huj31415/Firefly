using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firefly
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	internal class CameraManager : MonoBehaviour
	{
		public static CameraManager Instance { get; private set; }

		public bool isHdr = false;

		List<KeyValuePair<CameraEvent,CommandBuffer>> cameraBuffers = new List<KeyValuePair<CameraEvent,CommandBuffer>>();

		public bool ActualHdrState { get 
			{
				if (Camera.main != null) return Camera.main.allowHDR;
				else return isHdr;
			} 
		}

		public void Awake()
		{
			Instance = this;
			
			OverrideHDR(ConfigManager.Instance.modSettings.hdrOverride);
		}

		public void AddCommandBuffer(CameraEvent evt, CommandBuffer buf)
		{
			AddCommandBufferFlight(evt, buf);
			AddCommandBufferInternal(evt, buf);

			cameraBuffers.Add(new KeyValuePair<CameraEvent,CommandBuffer>(evt, buf));
		}

		public void RemoveCommandBuffer(CameraEvent evt, CommandBuffer buf)
		{
			FlightCamera.fetch.mainCamera?.RemoveCommandBuffer(evt, buf);
			InternalCamera.Instance?.GetComponent<Camera>().RemoveCommandBuffer(evt, buf);
		}

		void AddCommandBufferFlight(CameraEvent evt, CommandBuffer buf)
		{
			Camera flightCam = FlightCamera.fetch.mainCamera;
			flightCam?.AddCommandBuffer(evt, buf);
		}

		void AddCommandBufferInternal(CameraEvent evt, CommandBuffer buf)
		{
			Camera internalCam = InternalCamera.Instance?.GetComponent<Camera>();
			internalCam?.AddCommandBuffer(evt, buf);
		}

		public void OnCameraChange(global::CameraManager.CameraMode mode)
		{
			Camera flightCam = FlightCamera.fetch.mainCamera;
			Camera internalCam = InternalCamera.Instance?.GetComponent<Camera>();

			for (int i = 0; i < cameraBuffers.Count; i++)
			{
				CommandBuffer[] buffers = flightCam?.GetCommandBuffers(cameraBuffers[i].Key);
				if (!buffers.Contains(cameraBuffers[i].Value)) AddCommandBufferFlight(cameraBuffers[i].Key, cameraBuffers[i].Value);

				buffers = internalCam?.GetCommandBuffers(cameraBuffers[i].Key);
				if (!buffers.Contains(cameraBuffers[i].Value)) AddCommandBufferInternal(cameraBuffers[i].Key, cameraBuffers[i].Value);
			}
		}

		/// <summary>
		/// Sets the HDR option for the main and IVA cameras
		/// </summary>
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
