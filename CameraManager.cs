using System;
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
			for (int i = 0; i < cameraBuffers.Count; i++)
			{
				if (cameraBuffers[i].Key == evt && cameraBuffers[i].Value == buf) return;
			}

			AddCommandBufferFlight(evt, buf);
			AddCommandBufferInternal(evt, buf);

			cameraBuffers.Add(new KeyValuePair<CameraEvent,CommandBuffer>(evt, buf));
		}

		public void RemoveCommandBuffer(CameraEvent evt, CommandBuffer buf)
		{
			FlightCamera.fetch.mainCamera?.RemoveCommandBuffer(evt, buf);
			InternalCamera.Instance?.GetComponent<Camera>().RemoveCommandBuffer(evt, buf);

			for (int i = 0; i < cameraBuffers.Count; i++)
			{
				if (cameraBuffers[i].Key == evt && cameraBuffers[i].Value == buf)
				{
					cameraBuffers.RemoveAt(i);
					break;
				}
			}
		}

		void AddCommandBufferFlight(CameraEvent evt, CommandBuffer buf)
		{
			Camera flightCam = FlightCamera.fetch.mainCamera;
			if (flightCam == null) return;

			CommandBuffer[] buffers = flightCam.GetCommandBuffers(evt);
			if (buffers.Contains(buf)) return;  // detect duplicates

			flightCam.AddCommandBuffer(evt, buf);
		}

		void AddCommandBufferInternal(CameraEvent evt, CommandBuffer buf)
		{
			Camera internalCam = InternalCamera.Instance?.GetComponent<Camera>();
			if (internalCam == null) return;

			CommandBuffer[] buffers = internalCam.GetCommandBuffers(evt);
			if (buffers.Contains(buf)) return;  // detect duplicates

			internalCam.AddCommandBuffer(evt, buf);
		}

		public void OnCameraChange(global::CameraManager.CameraMode mode)
		{
			if (mode != global::CameraManager.CameraMode.IVA || mode != global::CameraManager.CameraMode.Internal) return;

			for (int i = 0; i < cameraBuffers.Count; i++)
			{ 
				AddCommandBufferInternal(cameraBuffers[i].Key, cameraBuffers[i].Value);
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
