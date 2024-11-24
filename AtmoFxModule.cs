using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Firefly
{
	/// <summary>
	/// Stores a pair of floats
	/// </summary>
	public struct FloatPair
	{
		public float x;
		public float y;

		public FloatPair(float x, float y)
		{
			this.x = x;
			this.y = y;
		}
	}

	/// <summary>
	/// Stores the data of an fx envelope renderer
	/// </summary>
	public struct FxEnvelopeModel
	{
		public string partName;
		public Renderer renderer;

		public FxEnvelopeModel(string partName, Renderer renderer)
		{
			this.partName = partName;
			this.renderer = renderer;
		}
	}

	/// <summary>
	/// Stores the data and instances of the effects
	/// </summary>
	public class AtmoFxVessel
	{
		public List<FxEnvelopeModel> fxEnvelope = new List<FxEnvelopeModel>();
		public List<Vector3> fxEnvelopeProperties = new List<Vector3>();
		public List<Renderer> fxEnvelopeGenerated = new List<Renderer>();
		public List<Renderer> particleFxEnvelope = new List<Renderer>();

		public CommandBuffer commandBuffer;

		public bool hasParticles = false;

		public List<ParticleSystem> allParticles = new List<ParticleSystem>();
		public List<FloatPair> orgParticleRates = new List<FloatPair>();
		public ParticleSystem sparkParticles;
		public ParticleSystem chunkParticles;
		public ParticleSystem alternateChunkParticles;
		public ParticleSystem smokeParticles;

		public Mesh totalEnvelope;

		public Camera airstreamCamera;
		public RenderTexture airstreamTexture;

		public Vector3[] vesselBounds = new Vector3[8];
		public Vector3 vesselBoundCenter;
		public Vector3 vesselBoundExtents;
		public Vector3 vesselMinCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		public Vector3 vesselMaxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		public float vesselBoundRadius;
		public float vesselMaxSize;

		public float lengthMultiplier = 1f;

		public Material material;
	}

	/// <summary>
	/// The module which manages the effects for each vessel
	/// </summary>
	public class AtmoFxModule : VesselModule
	{
		public AtmoFxVessel fxVessel;
		public bool isLoaded = false;

		bool debugMode = false;

		float lastFixedTime;
		float desiredRate;
		float lastSpeed;

		double vslLastAlt;

		public BodyConfig currentBody;

		// Snippet taken from Reentry Particle Effects by pizzaoverhead
		AerodynamicsFX _aeroFX;
		public AerodynamicsFX AeroFX
		{
			get
			{
				if (_aeroFX == null)
				{
					GameObject fxLogicObject = GameObject.Find("FXLogic");
					if (fxLogicObject != null)
						_aeroFX = fxLogicObject.GetComponent<AerodynamicsFX>();
				}
				return _aeroFX;
			}
		}

		int reloadDelayFrames= 0;

		public override Activation GetActivation()
		{
			return Activation.LoadedVessels | Activation.FlightScene;
		}

		/// <summary>
		/// Loads a vessel, instantiates stuff like the camera and rendertexture, also creates the entry velopes and particle system
		/// </summary>
		void CreateVesselFx()
		{
			if (!WindowManager.Instance.tgl_EffectToggle) return;

			// check if the vessel is actually loaded, and if it has any parts
			if (vessel == null || (!vessel.loaded) || vessel.parts.Count < 1 )
			{
				Logging.Log("Invalid vessel");
				Logging.Log($"loaded: {vessel.loaded}");
				Logging.Log($"partcount: {vessel.parts.Count}");
				Logging.Log($"atmo: {vessel.mainBody.atmosphere}");
				return;
			}

			if (isLoaded) return;

			// check for atmosphere
			if (!vessel.mainBody.atmosphere)
			{
				Logging.Log("MainBody does not have an atmosphere");
				return;
			}

			bool onModify = fxVessel != null;

			Logging.Log("Loading vessel " + vessel.name);
			Logging.Log(onModify ? "Using light method" : "Using heavy method");

			Material material;

			if (onModify)
			{
				material = fxVessel.material;
			}
			else
			{
				fxVessel = new AtmoFxVessel();

				// create material
				material = Instantiate(AssetLoader.Instance.globalMaterial);
				fxVessel.material = material;

				// create camera
				GameObject cameraGO = new GameObject("AtmoFxCamera - " + vessel.name);
				fxVessel.airstreamCamera = cameraGO.AddComponent<Camera>();

				fxVessel.airstreamCamera.orthographic = true;
				fxVessel.airstreamCamera.clearFlags = CameraClearFlags.SolidColor;
				fxVessel.airstreamCamera.cullingMask = (1 << 0);  // Only render layer 0, which is for the spacecraft

				// create rendertexture
				fxVessel.airstreamTexture = new RenderTexture(512, 512, 1, RenderTextureFormat.Depth);
				fxVessel.airstreamTexture.Create();
				fxVessel.airstreamCamera.targetTexture = fxVessel.airstreamTexture;
			}

			// Check if the fxVessel or material is null
			if (fxVessel == null || material == null)
			{
				Logging.Log("fxVessel/material is null");

				RemoveVesselFx(false);
				return;
			}

			// calculate the vessel bounds
			bool correctBounds = CalculateVesselBounds(fxVessel, vessel, true);
			if (!correctBounds)
			{
				Logging.Log("Recalculating invalid vessel bounds");
				CalculateVesselBounds(fxVessel, vessel, false);
			}
			fxVessel.airstreamCamera.orthographicSize = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude, 0.3f, 2000f);  // clamp the ortho camera size
			fxVessel.airstreamCamera.farClipPlane = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude * 2f, 1f, 1000f);  // set the far clip plane so the segment occlusion works

			// set the current body
			UpdateCurrentBody(vessel.mainBody, true);

			// create the command buffer
			InitializeCommandBuffer();

			// reset part cache
			ResetPartModelCache();

			// create the fx envelopes
			UpdateFxEnvelopes();
			fxVessel.material.SetTexture("_AirstreamTex", fxVessel.airstreamTexture);  // Set the airstream depth texture parameter

			// populate the command buffer
			PopulateCommandBuffer();

			// create the particles
			if (!(bool)ConfigManager.Instance.modSettings["disable_particles"]) CreateParticleSystems();  // run the function only if they're enabled in settings

			Logging.Log("Finished loading vessel");
			isLoaded = true;
		}

		/// <summary>
		/// Initializes the CommandBuffer for the vessel, and adds it to the cameras
		/// </summary>
		void InitializeCommandBuffer()
		{
			fxVessel.commandBuffer = new CommandBuffer();
			fxVessel.commandBuffer.name = $"Firefly atmospheric effects [{vessel.vesselName}]";
			fxVessel.commandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
			CameraManager.Instance.AddCommandBuffer(CameraEvent.AfterForwardAlpha, fxVessel.commandBuffer);
		}
		
		/// <summary>
		/// Populates the command buffer with the envelope
		/// </summary>
		void PopulateCommandBuffer()
		{
			fxVessel.commandBuffer.Clear();

			for (int i = 0; i < fxVessel.fxEnvelope.Count; i++)
			{
				FxEnvelopeModel envelope = fxVessel.fxEnvelope[i];

				// set model values
				fxVessel.commandBuffer.SetGlobalVector("_ModelScale", fxVessel.fxEnvelopeProperties[i*2]);
				fxVessel.commandBuffer.SetGlobalVector("_EnvelopeScaleFactor", fxVessel.fxEnvelopeProperties[i*2 + 1]);

				// part overrides
				BodyColors colors = new BodyColors(currentBody.colors);  // create the original colors
				if (ConfigManager.Instance.partConfigs.ContainsKey(envelope.partName))
				{
					Logging.Log("Envelope has a part override config");
					BodyColors overrideColor = ConfigManager.Instance.partConfigs[envelope.partName];

					// TODO: This is a mess, please clean up
					// TODO: Please don't ignore this todo
					// override the colors with the override
					if (overrideColor.glow.HasValue) colors.glow = overrideColor.glow;
					if (overrideColor.glowHot.HasValue) colors.glowHot = overrideColor.glowHot;

					if (overrideColor.trailPrimary.HasValue) colors.trailPrimary = overrideColor.trailPrimary;
					if (overrideColor.trailSecondary.HasValue) colors.trailSecondary = overrideColor.trailSecondary;
					if (overrideColor.trailTertiary.HasValue) colors.trailTertiary = overrideColor.trailTertiary;
					if (overrideColor.trailStreak.HasValue) colors.trailStreak = overrideColor.trailStreak;

					if (overrideColor.wrapLayer.HasValue) colors.wrapLayer = overrideColor.wrapLayer;
					if (overrideColor.wrapStreak.HasValue) colors.wrapStreak = overrideColor.wrapStreak;

					if (overrideColor.shockwave.HasValue) colors.shockwave = overrideColor.shockwave;
				}

				// add commands to set the color properties
				fxVessel.commandBuffer.SetGlobalColor("_GlowColor", colors.glow.Value);
				fxVessel.commandBuffer.SetGlobalColor("_HotGlowColor", colors.glowHot.Value);

				fxVessel.commandBuffer.SetGlobalColor("_PrimaryColor", colors.trailPrimary.Value);
				fxVessel.commandBuffer.SetGlobalColor("_SecondaryColor", colors.trailSecondary.Value);
				fxVessel.commandBuffer.SetGlobalColor("_TertiaryColor", colors.trailTertiary.Value);
				fxVessel.commandBuffer.SetGlobalColor("_StreakColor", colors.trailStreak.Value);

				fxVessel.commandBuffer.SetGlobalColor("_LayerColor", colors.wrapLayer.Value);
				fxVessel.commandBuffer.SetGlobalColor("_LayerStreakColor", colors.wrapStreak.Value);

				fxVessel.commandBuffer.SetGlobalColor("_ShockwaveColor", colors.shockwave.Value);

				// draw the mesh
				fxVessel.commandBuffer.DrawRenderer(envelope.renderer, fxVessel.material);
			}
		}

		/// <summary>
		/// Destroys and disposes the command buffer
		/// </summary>
		void DestroyCommandBuffer()
		{
			CameraManager.Instance.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, fxVessel.commandBuffer);
			fxVessel.commandBuffer.Dispose();
		}

		/// <summary>
		/// Resets the model renderer cache for each part
		/// </summary>
		void ResetPartModelCache()
		{
			for (int i = 0; i < vessel.parts.Count; i++)
			{
				vessel.parts[i].ResetModelRenderersCache();
			}
		}

		/// <summary>
		/// Processes one part and creates the envelope mesh for it
		/// </summary>
		void CreatePartEnvelope(Part part)
		{
			Transform[] fxEnvelopes = part.FindModelTransforms("atmofx_envelope");
			if (fxEnvelopes.Length > 0)
			{
				Logging.Log($"Part {part.name} has a defined effect envelope. Skipping collider search.");

				for (int j = 0; j < fxEnvelopes.Length; j++)
				{
					// check if active
					if (!fxEnvelopes[j].gameObject.activeInHierarchy) continue;

					if (!fxEnvelopes[j].TryGetComponent(out MeshFilter _)) continue;
					if (!fxEnvelopes[j].TryGetComponent(out MeshRenderer parentRenderer)) continue;

					parentRenderer.enabled = false;

					fxVessel.fxEnvelope.Add(new FxEnvelopeModel(Utils.GetPartCfgName(part.partInfo.name), parentRenderer));
					fxVessel.fxEnvelopeProperties.Add(Vector3.one);  //_ModelScale
					fxVessel.fxEnvelopeProperties.Add(Vector3.one);  //_EnvelopeScaleFactor

					if (Utils.IsPartBoundCompatible(part)) fxVessel.particleFxEnvelope.Add(parentRenderer);
				}

				// skip model search
				return;
			}

			// TODO: reminder that collider support is disabled for commandbuffer branch

			List<Renderer> models = part.FindModelRenderersCached();
			for (int j = 0; j < models.Count; j++)
			{
				Renderer model = models[j];

				// check if active
				if (!model.gameObject.activeInHierarchy) continue;

				// check for wheel flare
				if (Utils.CheckWheelFlareModel(part, model.gameObject.name)) continue;

				// check for layers
				if (Utils.CheckLayerModel(model.transform)) continue;

				// is skinned
				bool isSkinnedRenderer = model.TryGetComponent(out SkinnedMeshRenderer _);

				if (!isSkinnedRenderer)  // if it's a normal model, check if it has a filter and a mesh
				{
					// try getting the mesh filter
					bool hasMeshFilter = model.TryGetComponent(out MeshFilter filter);
					if (!hasMeshFilter) continue;

					// try getting the mesh
					Mesh mesh = filter.sharedMesh;
					if (mesh == null) continue;
				}

				if (!Utils.IsPartBoundCompatible(part)) continue;

				fxVessel.fxEnvelope.Add(new FxEnvelopeModel(Utils.GetPartCfgName(part.partInfo.name), model));
				fxVessel.fxEnvelopeProperties.Add(model.transform.lossyScale);  //_ModelScale
				fxVessel.fxEnvelopeProperties.Add(new Vector3(1.05f, 1.07f, 1.05f));  //_EnvelopeScaleFactor

				fxVessel.particleFxEnvelope.Add(model);
			}
		}

		/// <summary>
		/// Creates the effect envelopes
		/// </summary>
		void UpdateFxEnvelopes()
		{
			Logging.Log($"Updating fx envelopes for vessel {vessel.name}");
			Logging.Log($"Found {vessel.parts.Count} parts on the vessel");

			fxVessel.fxEnvelope.Clear();
			fxVessel.fxEnvelopeProperties.Clear();
			fxVessel.fxEnvelopeGenerated.Clear();
			fxVessel.particleFxEnvelope.Clear();

			for (int i = 0; i < vessel.parts.Count; i++)
			{
				Part part = vessel.parts[i];
				if (!Utils.IsPartCompatible(part)) continue;

				CreatePartEnvelope(part);
			}

			// set the vessel position to zero, to make combining possible
			Vector3 orgPosition = vessel.transform.position;
			vessel.transform.position = Vector3.zero;

			// combine the envelope meshes
			CombineInstance[] combine = new CombineInstance[fxVessel.particleFxEnvelope.Count];
			for (int i = 0; i < combine.Length; i++)
			{
				MeshFilter filter = fxVessel.particleFxEnvelope[i].GetComponent<MeshFilter>();
				if (filter == null) continue;

				// set the part position to match the vessel
				filter.transform.position -= orgPosition;

				combine[i].mesh = filter.sharedMesh;
				combine[i].transform = filter.transform.localToWorldMatrix;

				// reset the part position
				filter.transform.position += orgPosition;
			}
			fxVessel.totalEnvelope = new Mesh();
			fxVessel.totalEnvelope.CombineMeshes(combine);

			// reset the vessel position back to original
			vessel.transform.position = orgPosition;
		}

		/// <summary>
		/// Creates all particle systems for the vessel
		/// </summary>
		void CreateParticleSystems()
		{
			Logging.Log("Creating particle systems");

			fxVessel.hasParticles = true;

			for (int i = 0; i < vessel.transform.childCount; i++)
			{
				Transform t = vessel.transform.GetChild(i);

				// this is stupid, I don't know why this is neccessary but it is
				if (t.TryGetComponent(out ParticleSystem _)) Destroy(t.gameObject);
			}

			fxVessel.allParticles.Clear();
			fxVessel.orgParticleRates.Clear();

			// spawn particle systems
			fxVessel.sparkParticles = Instantiate(AssetLoader.Instance.sparkParticles, vessel.transform).GetComponent<ParticleSystem>();
			fxVessel.chunkParticles = Instantiate(AssetLoader.Instance.chunkParticles, vessel.transform).GetComponent<ParticleSystem>();
			fxVessel.alternateChunkParticles = Instantiate(AssetLoader.Instance.alternateChunkParticles, vessel.transform).GetComponent<ParticleSystem>();
			fxVessel.smokeParticles = Instantiate(AssetLoader.Instance.smokeParticles, vessel.transform).GetComponent<ParticleSystem>();

			// register the particle systems
			StoreParticleSystem(fxVessel.sparkParticles);
			StoreParticleSystem(fxVessel.chunkParticles);
			StoreParticleSystem(fxVessel.alternateChunkParticles);
			StoreParticleSystem(fxVessel.smokeParticles);

			// initialize particle systems
			fxVessel.sparkParticles.transform.rotation = Quaternion.identity;
			fxVessel.chunkParticles.transform.rotation = Quaternion.identity;
			fxVessel.alternateChunkParticles.transform.rotation = Quaternion.identity;
			fxVessel.smokeParticles.transform.rotation = Quaternion.identity;

			// disable if needed
			if ((bool)ConfigManager.Instance.modSettings["disable_sparks"]) fxVessel.sparkParticles.gameObject.SetActive(false);
			if ((bool)ConfigManager.Instance.modSettings["disable_debris"])
			{
				fxVessel.chunkParticles.gameObject.SetActive(false);
				fxVessel.alternateChunkParticles.gameObject.SetActive(false);
			}
			if ((bool)ConfigManager.Instance.modSettings["disable_smoke"]) fxVessel.smokeParticles.gameObject.SetActive(false);

			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				ParticleSystem ps = fxVessel.allParticles[i];

				ParticleSystem.ShapeModule shapeModule = ps.shape;
				shapeModule.mesh = fxVessel.totalEnvelope;

				ParticleSystem.VelocityOverLifetimeModule velocityModule = ps.velocityOverLifetime;
				velocityModule.radialMultiplier = 1f;

				UpdateParticleRate(ps, 0f, 0f);
			}
		}

		/// <summary>
		/// Stores the rate of a given particle system in the list
		/// </summary>
		void StoreParticleSystem(ParticleSystem ps)
		{
			fxVessel.allParticles.Add(ps);

			ParticleSystem.MinMaxCurve curve = ps.emission.rateOverTime;
			fxVessel.orgParticleRates.Add(new FloatPair(curve.constantMin, curve.constantMax));
		}

		/// <summary>
		/// Kills all particle systems
		/// </summary>
		void KillAllParticles()
		{
			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				UpdateParticleRate(fxVessel.allParticles[i], 0f, 0f);
			}
		}

		/// <summary>
		/// Updates the rate for a given particle system
		/// </summary>
		void UpdateParticleRate(ParticleSystem system, float min, float max)
		{
			ParticleSystem.EmissionModule emissionModule = system.emission;
			ParticleSystem.MinMaxCurve rateCurve = emissionModule.rateOverTime;

			rateCurve.constantMin = min;
			rateCurve.constantMax = max;

			emissionModule.rateOverTime = rateCurve;
		}

		/// <summary>
		/// Updates the velocity vector for a given particle system
		/// </summary>
		void UpdateParticleVel(ParticleSystem system, Vector3 dir, float min, float max)
		{
			ParticleSystem.VelocityOverLifetimeModule velocityModule = system.velocityOverLifetime;

			ParticleSystem.MinMaxCurve range = velocityModule.x;
			range.constantMin = dir.x * min;
			range.constantMax = dir.x * max;
			velocityModule.x = range;

			range = velocityModule.y;
			range.constantMin = dir.y * min;
			range.constantMax = dir.y * max;
			velocityModule.y = range;

			range = velocityModule.z;
			range.constantMin = dir.z * min;
			range.constantMax = dir.z * max;
			velocityModule.z = range;
		}

		/// <summary>
		/// Updates the particle systems, should be called everytime the velocity changes
		/// </summary>
		void UpdateParticleSystems()
		{
			// check if we should actually do the particles
			if (GetAdjustedEntrySpeed() < currentBody.particleThreshold)
			{
				KillAllParticles();
				return;
			}

			// rate
			desiredRate = Mathf.Clamp01((GetAdjustedEntrySpeed() - currentBody.particleThreshold) / 600f);
			for (int i = 0; i < fxVessel.allParticles.Count; i++)
			{
				ParticleSystem ps = fxVessel.allParticles[i];

				float min = fxVessel.orgParticleRates[i].x * desiredRate;
				float max = fxVessel.orgParticleRates[i].y * desiredRate;

				UpdateParticleRate(ps, min, max);
			}

			// world velocity
			Vector3 direction = vessel.transform.InverseTransformDirection(GetEntryVelocity());
			Vector3 worldVel = -GetEntryVelocity();

			// sparks	
			fxVessel.sparkParticles.transform.localPosition = direction * -0.5f * fxVessel.lengthMultiplier;

			// chunks
			fxVessel.chunkParticles.transform.localPosition = direction * -1.24f * fxVessel.lengthMultiplier;

			// alternate chunks
			fxVessel.alternateChunkParticles.transform.localPosition = direction * -1.62f * fxVessel.lengthMultiplier;

			// smoke
			fxVessel.smokeParticles.transform.localPosition = direction * -2f * Mathf.Max(fxVessel.lengthMultiplier * 0.5f, 1f);

			// directions
			UpdateParticleVel(fxVessel.sparkParticles, worldVel, 30f, 70f);
			UpdateParticleVel(fxVessel.chunkParticles, worldVel, 30f, 70f);
			UpdateParticleVel(fxVessel.alternateChunkParticles, worldVel, 15f, 20f);
			UpdateParticleVel(fxVessel.smokeParticles, worldVel, 125f, 135f);
		}

		/// <summary>
		/// Unloads the vessel, removing instances and other things like that
		/// </summary>
		public void RemoveVesselFx(bool onlyEnvelopes = false)
		{
			if (!isLoaded) return;

			isLoaded = false;

			Destroy(fxVessel.totalEnvelope);

			// destroy the commandbuffer
			DestroyCommandBuffer();

			fxVessel.fxEnvelope.Clear();
			fxVessel.fxEnvelopeProperties.Clear();
			fxVessel.particleFxEnvelope.Clear();

			if (!onlyEnvelopes)
			{
				// destroy the misc stuff
				if (fxVessel.material != null) Destroy(fxVessel.material);
				if (fxVessel.airstreamCamera != null) Destroy(fxVessel.airstreamCamera.gameObject);
				if (fxVessel.airstreamTexture != null) Destroy(fxVessel.airstreamTexture);

				// destroy the particles
				if (fxVessel.sparkParticles != null) Destroy(fxVessel.sparkParticles.gameObject);
				if (fxVessel.chunkParticles != null) Destroy(fxVessel.chunkParticles.gameObject);
				if (fxVessel.alternateChunkParticles != null) Destroy(fxVessel.alternateChunkParticles.gameObject);
				if (fxVessel.smokeParticles != null) Destroy(fxVessel.smokeParticles.gameObject);

				fxVessel = null;
			}

			Logging.Log("Unloaded vessel " + vessel.vesselName);
		}

		/// <summary>
		/// Reloads the vessel (simulates unloading and loading again)
		/// </summary>
		public void ReloadVessel()
		{
			RemoveVesselFx(false);
			reloadDelayFrames = Math.Max(reloadDelayFrames, 1);
		}

		/// <summary>
		/// Similar to ReloadVessel(), but it's much lighter since it does not re-instantiate the camera and particles
		/// </summary>
		public void OnVesselPartCountChanged()
		{
			// Mark the vessel for reloading
			RemoveVesselFx(true);
			reloadDelayFrames = Math.Max(reloadDelayFrames, 1);
		}

		public override void OnLoadVessel()
		{
			base.OnLoadVessel();

			reloadDelayFrames = 20;
		}

		public override void OnUnloadVessel()
		{
			base.OnUnloadVessel();

			RemoveVesselFx(false);
		}

		public void OnDestroy()
		{
			RemoveVesselFx(false);
		}

		void Debug_ToggleEnvelopes()
		{
			bool state = fxVessel.fxEnvelope[0].renderer.gameObject.activeSelf;

			for (int i = 0; i < fxVessel.fxEnvelope.Count; i++)
			{
				fxVessel.fxEnvelope[i].renderer.gameObject.SetActive(!state);
			}
		}

		public void Update()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			// debug mode
			if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha0) && vessel == FlightGlobals.ActiveVessel) debugMode = !debugMode;
			if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha8) && vessel == FlightGlobals.ActiveVessel) Debug_ToggleEnvelopes();
			if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha9) && vessel == FlightGlobals.ActiveVessel) ReloadVessel();

			// Reload if the vessel is marked for reloading
			if (reloadDelayFrames > 0 && vessel.loaded && !vessel.packed)
			{
				if (--reloadDelayFrames == 0)
				{
					CreateVesselFx();
				}
			}
		}

		public void LateUpdate()
		{
			// Certain things only need to happen if we had a fixed update
			if (Time.fixedTime != lastFixedTime && isLoaded)
			{
				lastFixedTime = Time.fixedTime;

				float entrySpeed = GetAdjustedEntrySpeed();

				// update particles
				if (fxVessel.hasParticles) UpdateParticleSystems();

				// position the cameras
				fxVessel.airstreamCamera.transform.position = GetOrthoCameraPosition();
				fxVessel.airstreamCamera.transform.LookAt(vessel.transform.TransformPoint(fxVessel.vesselBoundCenter));

				// view projection matrix for the airstream camera
				Matrix4x4 V = fxVessel.airstreamCamera.worldToCameraMatrix;
				Matrix4x4 P = GL.GetGPUProjectionMatrix(fxVessel.airstreamCamera.projectionMatrix, true);
				Matrix4x4 VP = P * V;

				// update the material with dynamic properties
				fxVessel.material.SetVector("_Velocity", GetEntryVelocity());
				fxVessel.material.SetFloat("_EntrySpeed", entrySpeed);
				fxVessel.material.SetMatrix("_AirstreamVP", VP);

				fxVessel.material.SetInt("_Hdr", CameraManager.Instance.ActualHdrState ? 1 : 0);
				fxVessel.material.SetFloat("_FxState", AeroFX.state);
				fxVessel.material.SetFloat("_AngleOfAttack", Utils.GetAngleOfAttack(vessel));
				fxVessel.material.SetFloat("_ShadowPower", 0f);
				fxVessel.material.SetFloat("_VelDotPower", 0f);
				fxVessel.material.SetFloat("_EntrySpeedMultiplier", 1f);
			}

			// Check if the ship goes outside of the atmosphere (and the speed is low enough), unload the effects if so
			if (vessel.altitude > vessel.mainBody.atmosphereDepth && isLoaded)
			{
				RemoveVesselFx(false);
			}

			// Check if the vessel is not marked for reloading and if it's entering the atmosphere
			double descentRate = vessel.altitude - vslLastAlt;
			vslLastAlt = vessel.altitude;
			if (reloadDelayFrames < 1 && descentRate < 0 && vessel.altitude <= vessel.mainBody.atmosphereDepth && !isLoaded)
			{
				CreateVesselFx();
			}
		}

		/// <summary>
		/// Debug drawings
		/// </summary>
		public void OnGUI()
		{
			if (!debugMode || !isLoaded) return;

			// vessel bounds
			Vector3[] vesselPoints = new Vector3[8];
			for (int i = 0; i < 8; i++)
			{
				vesselPoints[i] = vessel.transform.TransformPoint(fxVessel.vesselBounds[i]);
			}
			DrawingUtils.DrawBox(vesselPoints, Color.green);

			// vessel axes
			Vector3 fwd = vessel.GetFwdVector();
			Vector3 up = vessel.transform.up;
			Vector3 rt = Vector3.Cross(fwd, up);
			DrawingUtils.DrawAxes(vessel.transform.position, fwd, rt, up);

			// camera
			Transform camTransform = fxVessel.airstreamCamera.transform;
			DrawingUtils.DrawArrow(camTransform.position, camTransform.forward, camTransform.right, camTransform.up, Color.magenta);
		}

		/// <summary>
		/// Does the necessary stuff during an SOI change, like enabling/disabling the effects and changing the color configs
		/// Disables the effects on bodies without an atmosphere
		/// Enables the effects if necessary
		/// </summary>
		public void OnVesselSOIChanged(CelestialBody body)
		{
			if (!body.atmosphere)
			{
				RemoveVesselFx();
				return;
			}

			if (!isLoaded)
			{
				CreateVesselFx();
				return;
			}

			UpdateCurrentBody(body, false);
		}

		/// <summary>
		/// Updates the current body, and updates the properties
		/// </summary>
		private void UpdateCurrentBody(CelestialBody body, bool atLoad)
		{
			if (fxVessel != null)
			{
				Logging.Log($"Updating current body for {vessel.name}");

				ConfigManager.Instance.TryGetBodyConfig(body.name, true, out BodyConfig cfg);

				currentBody = cfg;
				fxVessel.lengthMultiplier = GetLengthMultiplier();
				UpdateStaticMaterialProperties();
				
				if (!atLoad)
				{
					// reset the commandbuffer
					DestroyCommandBuffer();
					InitializeCommandBuffer();
					PopulateCommandBuffer();
				}
			}
		}

		/// <summary>
		/// Updates the static material properties
		/// </summary>
		void UpdateStaticMaterialProperties()
		{
			fxVessel.material.SetFloat("_LengthMultiplier", fxVessel.lengthMultiplier);
			fxVessel.material.SetFloat("_OpacityMultiplier", currentBody.opacityMultiplier);
			fxVessel.material.SetFloat("_WrapFresnelModifier", currentBody.wrapFresnelModifier);

			fxVessel.material.SetFloat("_StreakProbability", currentBody.streakProbability);
			fxVessel.material.SetFloat("_StreakThreshold", currentBody.streakThreshold);
		}

		/// <summary>
		/// Calculates the total bounds of the entire vessel
		/// Returns if the calculation resulted in a correct bounding box
		/// </summary>
		bool CalculateVesselBounds(AtmoFxVessel fxVessel, Vessel vsl, bool doChecks)
		{
			// reset the corners
			fxVessel.vesselMaxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			fxVessel.vesselMinCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

			for (int i = 0; i < vsl.parts.Count; i++)
			{
				if ((!Utils.IsPartBoundCompatible(vsl.parts[i])) && doChecks) continue;

				List<Renderer> renderers = vsl.parts[i].FindModelRenderersCached();
				for (int r = 0; r < renderers.Count; r++)
				{
					// try getting the mesh filter
					bool hasFilter = renderers[r].TryGetComponent(out MeshFilter meshFilter);

					// is skinned
					bool isSkinnedRenderer = renderers[r].TryGetComponent(out SkinnedMeshRenderer skinnedModel);

					if (!isSkinnedRenderer)  // if it's a normal model, check if it has a filter and a mesh
					{
						if (!hasFilter) continue;
						if (meshFilter.mesh == null) continue;
					}

					// check if the mesh is legal
					if (Utils.CheckLayerModel(renderers[r].transform)) continue;

					// get the corners of the mesh
					Bounds modelBounds = isSkinnedRenderer ? skinnedModel.localBounds : meshFilter.mesh.bounds;
					Vector3[] corners = Utils.GetBoundCorners(modelBounds);

					// create the transformation matrix
					// part -> world -> vessel
					Matrix4x4 matrix = vsl.transform.worldToLocalMatrix * renderers[r].transform.localToWorldMatrix;

					Vector3[] vesselCorners = new Vector3[8];

					// iterate through each corner and multiply by the matrix
					for (int c = 0; c < 8; c++)
					{
						Vector3 v = matrix.MultiplyPoint3x4(corners[c]);

						vesselCorners[c] = v;

						// update the vessel bounds
						fxVessel.vesselMinCorner = Vector3.Min(fxVessel.vesselMinCorner, v);
						fxVessel.vesselMaxCorner = Vector3.Max(fxVessel.vesselMaxCorner, v);
					}
				}
			}

			if (fxVessel.vesselMaxCorner.x == float.MinValue) return false;  // return false if the corner hasn't changed

			Vector3 vesselSize = new Vector3(
				Mathf.Abs(fxVessel.vesselMaxCorner.x - fxVessel.vesselMinCorner.x),
				Mathf.Abs(fxVessel.vesselMaxCorner.y - fxVessel.vesselMinCorner.y),
				Mathf.Abs(fxVessel.vesselMaxCorner.z - fxVessel.vesselMinCorner.z)
			);

			Bounds bounds = new Bounds(fxVessel.vesselMinCorner + vesselSize / 2f, vesselSize);

			fxVessel.vesselBounds = Utils.GetBoundCorners(bounds);
			fxVessel.vesselMaxSize = Mathf.Max(vesselSize.x, vesselSize.y, vesselSize.z);
			fxVessel.vesselBoundCenter = bounds.center;
			fxVessel.vesselBoundExtents = vesselSize / 2f;
			fxVessel.vesselBoundRadius = fxVessel.vesselBoundExtents.magnitude;

			return true;
		}

		/// <summary>
		/// Returns the velocity direction
		/// </summary>
		Vector3 GetEntryVelocity()
		{
			return vessel.srf_velocity.normalized;
		}

		/// <summary>
		/// Returns the speed of the airflow, based on the mach number and static pressure
		/// </summary>
		float GetEntrySpeed()
		{
			/*
			// get the vessel speed in mach (yes, this is pretty much the same as normal m/s measurement, but it automatically detects a vacuum)
			double mach = vessel.mainBody.GetSpeedOfSound(vessel.staticPressurekPa, vessel.atmDensity);
			double vesselMach = vessel.mach;

			// get the stock aeroFX scalar
			float aeroFxScalar = AeroFX.FxScalar + 0.26f;  // adding 0.26 and body scalar to make the effect start earlier

			// apply the body config modifiers
			for (int i = 0; i < currentBody.transitionModifiers.Length; i++)
			{
				TransitionModifier mod = currentBody.transitionModifiers[i];

				float value = mod.value * (mod.stockfxDependent ? (1f - AeroFX.FxScalar) : 1f);

				switch (mod.operation)
				{
					case ModifierOperation.ADD:
						aeroFxScalar += value;
						break;
					case ModifierOperation.SUBTRACT:
						aeroFxScalar -= value;
						break;
					case ModifierOperation.MULTIPLY:
						aeroFxScalar *= Mathf.Max(value, mod.stockfxDependent ? 1f : 0f);  // if the effect is stockfx dependent then clamp it to not go below 1
						break;
					case ModifierOperation.DIVIDE:
						aeroFxScalar /= value;
						break;
					default:
						break;
				}
			}

			// convert to m/s
			spd = (float)(mach * vesselMach);
			spd = (float)(spd * vessel.srf_velocity.normalized.magnitude);
			spd *= aeroFxScalar;
			*/

			float spd = AeroFX.FxScalar * 2800f * Mathf.Lerp(0.13f, 1f, AeroFX.state);

			float delta = Mathf.Abs(spd - lastSpeed) / 2800f;
			spd = Mathf.Lerp(lastSpeed, spd, TimeWarp.deltaTime * (1f + delta * 2f));

			lastSpeed = spd;

			return spd;
		}

		/// <summary>
		/// Calculates the speed multiplier
		/// </summary>
		float GetLengthMultiplier()
		{
			// the Apollo capsule has a radius of around 2, which makes it a good reference
			float baseRadius = fxVessel.vesselBoundRadius / 2f;

			// gets the final result
			// for example, if the base radius is 2 then the result will be 1.4
			// or if the base radius is 3 then the result will be 1.8
			float result = 1f + (baseRadius - 1f) * 0.3f;

			return result * currentBody.lengthMultiplier;
		}

		/// <summary>
		/// Returns the entry speed adjusted to the atmosphere parameters
		/// </summary>
		public float GetAdjustedEntrySpeed()
		{
			/*
			if (WindowManager.Instance.tgl_SpeedMethod)
			{
				return GetEntrySpeed() * fxVessel.speedMultiplier * currentBody.intensity;
			}
			*/

			return GetEntrySpeed() * currentBody.strengthMultiplier;
		}

		/// <summary>
		/// Returns the camera position adjusted for an orhtographic projection
		/// </summary>
		Vector3 GetOrthoCameraPosition()
		{
			float maxExtent = fxVessel.vesselBoundRadius;
			float distance = maxExtent * 1.1f;

			Vector3 localDir = vessel.transform.InverseTransformDirection(GetEntryVelocity());
			Vector3 localPos = fxVessel.vesselBoundCenter + distance * localDir;

			return vessel.transform.TransformPoint(localPos);
		}
	}
}
