using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AtmosphericFx
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
	/// Stores the data and instances of the effects
	/// </summary>
	public class AtmoFxVessel
	{
		public List<Renderer> fxEnvelope = new List<Renderer>();
		public List<Renderer> particleFxEnvelope = new List<Renderer>();

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
		public float speedMultiplier = 1f;

		public Material material;
	}

	/// <summary>
	/// The module which manages the effects for each vessel
	/// </summary>
	public class AtmoFxModule : VesselModule
	{
		public AtmoFxVessel fxVessel = new AtmoFxVessel();
		bool isLoaded = false;

		bool debugMode = false;

		float desiredRate;
		float lastSpeed;

		public BodyConfig currentBody;

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

		/// <summary>
		/// Loads a vessel, instantiates stuff like the camera and rendertexture, also creates the entry velopes and particle system
		/// </summary>
		void OnVesselLoaded()
		{
			// check if the vessel is actually loaded, and if it has any parts
			if ((!vessel.loaded) || vessel.parts.Count < 1)
			{
				EventManager.UnregisterInstance(vessel.id);
				return;
			}

			isLoaded = true;
			fxVessel = new AtmoFxVessel();
			Logging.Log("Loading vessel " + vessel.name);

			// create material
			Material material = Instantiate(AssetLoader.Instance.globalMaterial);
			fxVessel.material = material;

			// create camera
			GameObject cameraGO = new GameObject("AtmoFxCamera - " + vessel.name);
			fxVessel.airstreamCamera = cameraGO.AddComponent<Camera>();

			fxVessel.airstreamCamera.orthographic = true;
			fxVessel.airstreamCamera.clearFlags = CameraClearFlags.SolidColor;
			fxVessel.airstreamCamera.cullingMask = (1 << 0);

			// create rendertexture
			fxVessel.airstreamTexture = new RenderTexture(512, 512, 1, RenderTextureFormat.Depth);
			fxVessel.airstreamTexture.Create();
			fxVessel.airstreamCamera.targetTexture = fxVessel.airstreamTexture;

			// reset part cache
			ResetPartModelCache();

			// create the fx envelopes
			UpdateFxEnvelopes(material);
			fxVessel.material.SetTexture("_AirstreamTex", fxVessel.airstreamTexture);

			// create the particles
			CreateParticleSystems();

			// calculate the vessel bounds
			CalculateVesselBounds(fxVessel, vessel);
			fxVessel.airstreamCamera.orthographicSize = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude, 0.3f, 2000f);
			fxVessel.airstreamCamera.farClipPlane = Mathf.Clamp(fxVessel.vesselBoundExtents.magnitude * 2f, 1f, 1000f);

			// set the current body
			currentBody = ConfigManager.Instance.GetVesselBody(vessel);

			// calculate the length multiplier
			fxVessel.lengthMultiplier = GetLengthMultiplier();

			// update the material properties
			UpdateMaterialProperties();

			Logging.Log("Finished loading vessel");
		}

		void ResetPartModelCache()
		{
			for (int i = 0; i < vessel.parts.Count; i++)
			{
				vessel.parts[i].ResetModelRenderersCache();
			}
		}

		/// <summary>
		/// Creates one envelope mesh, with a given parent, mesh and material
		/// </summary>
		MeshRenderer InstantiateEnvelopeMesh(Transform parent, Mesh mesh, Material material)
		{
			// create envelope object
			Transform envelope = new GameObject("atmofx_envelope_generated").transform;
			envelope.gameObject.layer = AtmoFxLayers.Fx;
			envelope.parent = parent;

			envelope.localPosition = Vector3.zero;
			envelope.localRotation = Quaternion.identity;
			envelope.localScale = new Vector3(1.2f, 1.2f, 1.2f);
			//envelope.localScale = new Vector3(1.05f, 1.07f, 1.05f);

			// add mesh filter and renderer to the envelope
			MeshFilter filter = envelope.gameObject.AddComponent<MeshFilter>();
			MeshRenderer renderer = envelope.gameObject.AddComponent<MeshRenderer>();

			// initialize renderer
			filter.mesh = mesh;
			renderer.sharedMaterial = material;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			// set model-specific properties
			MaterialPropertyBlock properties = new MaterialPropertyBlock();
			//properties.SetVector("_ModelScale", parent.lossyScale);
			properties.SetVector("_ModelScale", envelope.lossyScale);
			renderer.SetPropertyBlock(properties);

			return renderer;
		}

		/// <summary>
		/// Processes one part and creates the envelope mesh for it
		/// </summary>
		void CreatePartEnvelope(Part part, Material material)
		{
			StartCoroutine(CreatePartEnvelopeCoroutine(part, material));
		}

		/// <summary>
		/// Coroutine for the CreatePartEnvelope function, spreads the generation over multiple frames
		/// </summary>
		IEnumerator CreatePartEnvelopeCoroutine(Part part, Material material)
		{
			Transform[] fxEnvelopes = part.FindModelTransforms("atmofx_envelope");
			if (fxEnvelopes.Length > 0)
			{
				Logging.Log($"Part {part.name} has a defined effect envelope. Skipping collider search.");

				for (int j = 0; j < fxEnvelopes.Length; j++)
				{
					if (!fxEnvelopes[j].TryGetComponent(out MeshFilter parentFilter)) continue;

					// disable the mesh
					if (fxEnvelopes[j].TryGetComponent(out MeshRenderer parentRenderer)) parentRenderer.enabled = false;

					// needs to be a separate transform, otherwise it breaks for some reason
					MeshRenderer r = InstantiateEnvelopeMesh(fxEnvelopes[j], parentFilter.mesh, material);
					fxVessel.fxEnvelope.Add(r);

					if (IsPartBoundCompatible(part)) fxVessel.particleFxEnvelope.Add(r);

					yield return null;
				}

				// skip model search
				yield break;
			}

			// TODO: Uncomment and make this a setting
			/*
			Collider[] colliders = part.GetPartColliders();
			for (int j = 0; j < colliders.Length; j++)
			{
				MeshCollider collider = colliders[j] as MeshCollider;
				if (collider == null)
				{
					Logging.Log($"Collider {colliders[j].gameObject.name} isn't a mesh, ignoring");
					continue;
				}

				MeshRenderer renderer = InstantiateEnvelopeMesh(collider.transform, collider.sharedMesh, material);
				fxVessel.fxEnvelope.Add(renderer);

				Logging.Log($"Initialized collider {colliders[j].gameObject.name}");
			}
			*/

			List<Renderer> models = part.FindModelRenderersCached();
			for (int j = 0; j < models.Count; j++)
			{
				Renderer model = models[j];

				// check for wheel flare
				if (CheckWheelFlareModel(part, model.gameObject.name)) continue;

				// check for layers
				if (CheckLayerModel(model.transform)) continue;

				// try getting the mesh filter
				bool hasMeshFilter = model.TryGetComponent(out MeshFilter filter);
				if (!hasMeshFilter) continue;

				// try getting the mesh
				Mesh mesh = filter.sharedMesh;
				if (mesh == null) continue;

				MeshRenderer renderer = InstantiateEnvelopeMesh(model.transform, mesh, material);
				fxVessel.fxEnvelope.Add(renderer);

				if (IsPartBoundCompatible(part)) fxVessel.particleFxEnvelope.Add(renderer);
			}
		}

		/// <summary>
		/// Creates the effect envelopes
		/// </summary>
		void UpdateFxEnvelopes(Material material)
		{
			Logging.Log($"Updating fx envelopes for vessel {vessel.name}");
			Logging.Log($"Found {vessel.parts.Count} parts on the vessel");

			fxVessel.fxEnvelope.Clear();
			fxVessel.particleFxEnvelope.Clear();

			for (int i = 0; i < vessel.parts.Count; i++)
			{
				Part part = vessel.parts[i];
				if (!IsPartCompatible(part)) continue;

				CreatePartEnvelope(part, material);
			}

			// set the vessel position to zero, to make combining possible
			Vector3 orgPosition = vessel.transform.position;
			vessel.transform.position = Vector3.zero;

			// combine the envelope meshes
			CombineInstance[] combine = new CombineInstance[fxVessel.particleFxEnvelope.Count];
			for (int i = 0; i < combine.Length; i++)
			{
				MeshFilter filter = fxVessel.particleFxEnvelope[i].GetComponent<MeshFilter>();

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
		public void OnVesselUnload()
		{
			isLoaded = false;

			// destroy the fx envelope
			for (int i = 0; i < fxVessel.fxEnvelope.Count; i++)
			{
				if (fxVessel.fxEnvelope[i] != null) Destroy(fxVessel.fxEnvelope[i].gameObject);
			}

			// destroy the misc stuff
			if (fxVessel.material != null) Destroy(fxVessel.material);
			if (fxVessel.airstreamCamera != null) Destroy(fxVessel.airstreamCamera.gameObject);
			if (fxVessel.airstreamTexture != null) Destroy(fxVessel.airstreamTexture);

			// destroy the particles
			if (fxVessel.sparkParticles != null) Destroy(fxVessel.sparkParticles.gameObject);
			if (fxVessel.chunkParticles != null) Destroy(fxVessel.chunkParticles.gameObject);
			if (fxVessel.alternateChunkParticles != null) Destroy(fxVessel.alternateChunkParticles.gameObject);
			if (fxVessel.smokeParticles != null) Destroy(fxVessel.smokeParticles.gameObject);

			Logging.Log("Unloaded vessel " + vessel.vesselName);
		}

		/// <summary>
		/// Reloads the vessel (simulates unloading and loading again)
		/// </summary>
		public void ReloadVessel()
		{
			OnVesselUnload();
			OnVesselLoaded();
		}

		/// <summary>
		/// Coroutine which starts the OnVesselLoaded() method after 10 frames
		/// </summary>
		IEnumerator LoadVesselCoroutine()
		{
			for (int i = 0; i < 10; i++)
			{
				yield return null;
			}

			OnVesselLoaded();
		}

		public void Awake()
		{
			base.Awake();
		}

		public override void OnLoadVessel()
		{
			base.OnLoadVessel();

			if (!AssetLoader.Instance.allAssetsLoaded) return;
			
			EventManager.RegisterInstance(vessel.id, this);

			StartCoroutine(LoadVesselCoroutine());
		}

		public override void OnUnloadVessel()
		{
			base.OnUnloadVessel();

			if (!AssetLoader.Instance.allAssetsLoaded) return;

			OnVesselUnload();
		}

		public void OnDestroy()
		{
			OnVesselUnload();
		}

		void Debug_ToggleEnvelopes()
		{
			bool state = fxVessel.fxEnvelope[0].gameObject.activeSelf;

			for (int i = 0; i < fxVessel.fxEnvelope.Count; i++)
			{
				fxVessel.fxEnvelope[i].gameObject.SetActive(!state);
			}
		}

		public void Update()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			// debug mode
			if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha0) && vessel == FlightGlobals.ActiveVessel) debugMode = !debugMode;
			if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha9) && vessel == FlightGlobals.ActiveVessel) ReloadVessel();
			if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha8) && vessel == FlightGlobals.ActiveVessel) Debug_ToggleEnvelopes();
		}

		public void FixedUpdate()
		{
			if (!AssetLoader.Instance.allAssetsLoaded) return;

			// return if the vessel isnt loaded
			if ((!vessel.loaded) || (!isLoaded)) return;

			// update particles
			UpdateParticleSystems();

			// position the cameras
			fxVessel.airstreamCamera.transform.position = GetOrthoCameraPosition();
			fxVessel.airstreamCamera.transform.LookAt(vessel.transform.TransformPoint(fxVessel.vesselBoundCenter));

			// view projection matrix for the airstream camera
			Matrix4x4 V = fxVessel.airstreamCamera.worldToCameraMatrix;
			Matrix4x4 P = GL.GetGPUProjectionMatrix(fxVessel.airstreamCamera.projectionMatrix, true);
			Matrix4x4 VP = P * V;

			// update the material with dynamic properties
			fxVessel.material.SetVector("_Velocity", GetEntryVelocity());
			fxVessel.material.SetFloat("_EntrySpeed", GetAdjustedEntrySpeed());
			fxVessel.material.SetMatrix("_AirstreamVP", VP);

			fxVessel.material.SetFloat("_FxState", AeroFX.state);
			fxVessel.material.SetFloat("_AngleOfAttack", GetAngleOfAttack());
			fxVessel.material.SetFloat("_ShadowPower", 0f);
			fxVessel.material.SetFloat("_VelDotPower", 0f);
			fxVessel.material.SetFloat("_EntrySpeedMultiplier", 1f);
		}

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
		/// Updates the current body, and updates the properties
		/// </summary>
		public void UpdateCurrentBody(BodyConfig cfg)
		{
			currentBody = cfg;
			fxVessel.lengthMultiplier = GetLengthMultiplier();
			UpdateMaterialProperties();
		}

		/// <summary>
		/// Updates the colors of the material
		/// </summary>
		void UpdateMaterialProperties()
		{
			fxVessel.material.SetFloat("_LengthMultiplier", fxVessel.lengthMultiplier);
			fxVessel.material.SetFloat("_OpacityMultiplier", currentBody.opacityMultiplier);
			fxVessel.material.SetFloat("_WrapFresnelModifier", currentBody.wrapFresnelModifier);

			fxVessel.material.SetFloat("_StreakProbability", currentBody.streakProbability);
			fxVessel.material.SetFloat("_StreakThreshold", currentBody.streakThreshold);

			fxVessel.material.SetColor("_GlowColor", currentBody.colors.glow);
			fxVessel.material.SetColor("_HotGlowColor", currentBody.colors.glowHot);

			fxVessel.material.SetColor("_PrimaryColor", currentBody.colors.trailPrimary);
			fxVessel.material.SetColor("_SecondaryColor", currentBody.colors.trailSecondary);
			fxVessel.material.SetColor("_TertiaryColor", currentBody.colors.trailTertiary);

			fxVessel.material.SetColor("_LayerColor", currentBody.colors.wrapLayer);

			fxVessel.material.SetColor("_ShockwaveColor", currentBody.colors.shockwave);
		}

		/// <summary>
		/// Returns the corners of a given Bounds object
		/// </summary>
		Vector3[] GetBoundCorners(Bounds bounds)
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
		/// Calculates the total bounds of the entire vessel
		/// </summary>
		void CalculateVesselBounds(AtmoFxVessel fxVessel, Vessel vsl)
		{
			// reset the corners
			fxVessel.vesselMaxCorner = new Vector3(float.MinValue, float.MinValue, float.MinValue);
			fxVessel.vesselMinCorner = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

			for (int i = 0; i < vsl.parts.Count; i++)
			{
				if (!IsPartBoundCompatible(vsl.parts[i])) continue;

				List<Renderer> renderers = vsl.parts[i].FindModelRenderersCached();
				for (int r = 0; r < renderers.Count; r++)
				{
					bool hasFilter = renderers[r].TryGetComponent(out MeshFilter meshFilter);
					if (!hasFilter) continue;
					if (meshFilter.mesh == null) continue;

					// check if the mesh is legal
					if (CheckLayerModel(renderers[r].transform)) continue;

					// get the corners of the mesh
					//meshFilter.mesh.RecalculateBounds();
					Vector3[] corners = GetBoundCorners(meshFilter.mesh.bounds);

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

			Vector3 vesselSize = new Vector3(
				Mathf.Abs(fxVessel.vesselMaxCorner.x - fxVessel.vesselMinCorner.x),
				Mathf.Abs(fxVessel.vesselMaxCorner.y - fxVessel.vesselMinCorner.y),
				Mathf.Abs(fxVessel.vesselMaxCorner.z - fxVessel.vesselMinCorner.z)
			);

			Bounds bounds = new Bounds(fxVessel.vesselMinCorner + vesselSize / 2f, vesselSize);

			fxVessel.vesselBounds = GetBoundCorners(bounds);
			fxVessel.vesselMaxSize = Mathf.Max(vesselSize.x, vesselSize.y, vesselSize.z);
			fxVessel.vesselBoundCenter = bounds.center;
			fxVessel.vesselBoundExtents = vesselSize / 2f;
			fxVessel.vesselBoundRadius = fxVessel.vesselBoundExtents.magnitude;
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
			// get the vessel speed in mach (yes, this is pretty much the same as normal m/s measurement, but it automatically detects a vacuum)
			double mach = vessel.mainBody.GetSpeedOfSound(vessel.staticPressurekPa, vessel.atmDensity);
			double vesselMach = vessel.mach;

			// get the stock aeroFX scalar
			float aeroFxScalar = AeroFX.FxScalar + 0.26f;  // adding 0.26 and body scalar to make the effect start earlier

			// apply the body config modifiers
			for (int i = 0; i < currentBody.transitionModifiers.Length; i++)
			{
				TransitionModifier mod = currentBody.transitionModifiers[i];

				switch (mod.operation)
				{
					case ModifierOperation.ADD:
						aeroFxScalar += mod.value;
						break;
					case ModifierOperation.SUBTRACT:
						aeroFxScalar -= mod.value;
						break;
					case ModifierOperation.MULTIPLY:
						aeroFxScalar *= mod.value;
						break;
					case ModifierOperation.DIVIDE:
						aeroFxScalar /= mod.value;
						break;
					default:
						break;
				}
			}

			// convert to m/s
			float spd = (float)(mach * vesselMach);
			spd = (float)(spd * vessel.srf_velocity.normalized.magnitude);
			spd *= aeroFxScalar;
			spd = Mathf.Lerp(lastSpeed, spd, TimeWarp.deltaTime);

			lastSpeed = spd;

			return spd;
		}

		/// <summary>
		/// Returns the angle of attack
		/// </summary>
		float GetAngleOfAttack()
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
		/// Calculates the speed multiplier
		/// </summary>
		float GetLengthMultiplier()
		{
			// the Apollo capsule has a radius of around 2, which makes it a good reference
			float baseRadius = fxVessel.vesselBoundRadius / 2f;

			// gets the final result
			// for example, if the base radius is 2 then the result will be 1.4
			// or if the base radius is 3 then the result will be 1.8
			float result = 1f + (baseRadius - 1f) * 0.4f;

			return result * currentBody.lengthMultiplier;
		}

		/// <summary>
		/// Returns the entry speed adjusted to the atmosphere parameters
		/// </summary>
		public float GetAdjustedEntrySpeed()
		{
			return GetEntrySpeed() * fxVessel.speedMultiplier * currentBody.intensity;
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

		/// <summary>
		/// Is part legible for bound calculations?
		/// </summary>
		bool IsPartBoundCompatible(Part part)
		{
			return IsPartCompatible(part) && !(
				part.Modules.Contains("ModuleParachute")
			);
		}

		/// <summary>
		/// Is part legible for fx envelope calculations?
		/// </summary>
		bool IsPartCompatible(Part part)
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
		bool CheckWheelFlareModel(Part part, string model)
		{
			bool isFlare = model.ToLower().Equals("flare");
			bool isWheel = part.Modules.Contains("ModuleWheelBase");

			return isFlare && isWheel;
		}

		/// <summary>
		/// Check if a model's layer is illegal
		/// </summary>
		bool CheckLayerModel(Transform model)
		{
			return (
				model.gameObject.layer == 1
			);
		}

		/// <summary>
		/// Destroy an object, only if it exists
		/// </summary>
		void DestroyConditional(Object o)
		{
			if (o != null) Destroy(o);
		}
	}
}
