Shader "MirageDev/AtmosphericEntry"
{
    Properties
    {
		[Header(Textures)]
        _AirstreamTex ("Airstream Texture", 2D) = "" {}
		_NoiseTex ("Noise Texture", 2D) = "" {}
		_DitherTex ("Dither Texture", 2D) = "" {}

		[Space]
		[Header(Values)]
		_TrailAlphaMultiplier ("Trail Alpha Multiplier", Float) = 1
		_BlueMultiplier ("Blue Multiplier", Float) = 0.1
		_HeatMultiplier ("Heat Multiplier", Float) = 1
		_OpacityMultiplier ("Opacity Multiplier", Float) = 1
		_WrapFresnelModifier ("Wrap layer fresnel modifier", Float) = 0
		_StreakProbability ("Streak Probability", Float) = 0.1
		_StreakThreshold ("Streak Threshold", Float) = -0.2
		
		// Colors
		[Space]
		[Header(Colors)]
		[HDR] _GlowColor ("Glow Color", Color) = (0, 0, 0)
		[HDR] _HotGlowColor ("Hot Glow Color", Color) = (0, 0, 0)
		[HDR] _PrimaryColor ("Primary Trail Color", Color) = (0, 0, 0)
		[HDR] _SecondaryColor ("Secondary Trail Color", Color) = (0, 0, 0)
		[HDR] _TertiaryColor ("Tertiary Trail Color", Color) = (0, 0, 0)
		[HDR] _LayerColor ("Second Layer Color", Color) = (0, 0, 0)
		[HDR] _LayerStreakColor ("Second Layer Streak Color", Color) = (0, 0, 0)
		[HDR] _ShockwaveColor ("Shockwave Color", Color) = (0, 0, 0)
    }

    SubShader
    {
		Tags { "Queue"="Transparent" "IgnoreProjector" = "True" }
		LOD 100

		HLSLINCLUDE

		#include "UnityCG.cginc"

		Texture2D _AirstreamTex;
		SamplerState sampler_AirstreamTex;

		Texture2D _NoiseTex;
		SamplerState sampler_NoiseTex;
		
		Texture2D _DitherTex;
		SamplerState sampler_DitherTex;
		
		float _EntrySpeed;
		float _EntrySpeedMultiplier;
		
		float3 _ModelScale;
		float3 _Velocity;
		float4x4 _AirstreamVP;

		// Gets the scaled entry speed
		float GetEntrySpeed()
		{
			return _EntrySpeed * _EntrySpeedMultiplier;
		}
		float Shadow(float3 airstreamNDC, float bias, float shadowStrength)
		{
			if (airstreamNDC.x < -1.0f || airstreamNDC.x > 1.0f || airstreamNDC.y < -1.0f || airstreamNDC.y > 1.0f) 
			{
				return 1;
			}
				
			float2 UV = airstreamNDC.xy * 0.5 + 0.5;
			float3 lpos = airstreamNDC;
			
			#if defined(UNITY_REVERSED_Z)
				
			#else
				lpos.z = -lpos.z * 0.5 + 0.5;
			#endif
			
			#if UNITY_UV_STARTS_AT_TOP
				UV.y = 1 - UV.y;
			#endif
			
			lpos.x = lpos.x / 2 + 0.5;
			lpos.y = lpos.y / -2 + 0.5;
			lpos.z -= bias;
				
			float sum = 0;
			float x, y;
			for (y = -2; y <= 2; y += 1.0f)
			{
				for (x = -2; x <= 2; x += 1.0f)
				{
					float sampled = _AirstreamTex.SampleLevel(sampler_AirstreamTex, UV + float2(x * 1/512, y * 1/512), 0);
					
					#if defined(UNITY_REVERSED_Z)
						
					#else
						sampled = 1 - sampled;
					#endif
					
					if (sampled <= lpos.z) sum += 1;
				}
			}
			float shadowFac = sum / 25.0f;
			return saturate(shadowFac + 1 - shadowStrength);
		}
		
		float3 TransformObjectToWorld(float3 v) 
		{
			return mul(unity_ObjectToWorld, float4(v, 1.0));
		}

		float3 TransformWorldToObject(float3 v) 
		{
			return mul(unity_WorldToObject, float4(v, 1.0));
		}
		
		float3 GetAirstreamNDC(float3 positionOS)
		{
			float3 positionWS = TransformObjectToWorld(positionOS);

			float4 airstreamPosition = mul(_AirstreamVP, float4(positionWS, 1));
			return airstreamPosition.xyz / airstreamPosition.w;
		}
		
		// Samples the noise map at a given UV and channel
		float Noise(float2 uv, int channel)
		{
			return _NoiseTex.SampleLevel(sampler_NoiseTex, uv + float2(_Time.x*8, _Time.x*4), 0)[channel];
		}
		
		float NoiseStatic(float2 uv, int channel)
		{
			return _NoiseTex.SampleLevel(sampler_NoiseTex, uv, 0)[channel];
		}
		
		float Fresnel(float3 normal, float3 viewDir, float power) 
		{
			return pow((1.0 - saturate(dot(normalize(normal), normalize(viewDir)))), power);
		}
		
		// The 2 functions below are written with the MIT license by gkjohnson - https://github.com/gkjohnson/unity-dithered-transparency-shader
		/*
		MIT License
		Copyright (c) 2017 Garrett Johnson
		Permission is hereby granted, free of charge, to any person obtaining a copy
		of this software and associated documentation files (the "Software"), to deal
		in the Software without restriction, including without limitation the rights
		to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
		copies of the Software, and to permit persons to whom the Software is
		furnished to do so, subject to the following conditions:
		The above copyright notice and this permission notice shall be included in all
		copies or substantial portions of the Software.
		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
		IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
		AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
		LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
		OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
		SOFTWARE.
		*/
		float Dither(float alpha, float2 pos)
		{
			pos *= _ScreenParams.xy;

			float DITHER_THRESHOLDS[16] =
			{
				1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
				13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
				4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
				16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
			};

			int index = (int(pos.x) % 4) * 4 + int(pos.y) % 4;
			return alpha - DITHER_THRESHOLDS[index];
		}
		float DitherTexture(float alpha, float2 pos)
		{
			pos *= _ScreenParams.xy;

			pos.xy *= 1.0 / 470.0;

			float texValue = _DitherTex.SampleLevel(sampler_DitherTex, pos, 0).r;

			// ensure that we clip if the alpha is zero by
			// subtracting a small value when alpha == 0, because
			// the clip function only clips when < 0
			return alpha - texValue - 0.0001 * (1 - ceil(alpha));
		}

		ENDHLSL

        Pass
        {
			Name "Main Pass"

			Tags { "Queue"="Transparent" "RenderType" = "Transparent" }
			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha
			
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct VS_INPUT
            {
                float4 position : POSITION;
				float3 normal : NORMAL;
            };

            struct FS_INPUT
            {
                float4 position : SV_POSITION;
				float3 normal : NORMAL;
				
				float4 screenPos : TEXCOORD2;
				float3 worldPosition : TEXCOORD3;
				float4 airstreamNDC : TEXCOORD4;
				float3 viewDir : TEXCOORD5;
            };
			
			float4 _GlowColor;
			float4 _HotGlowColor;
			float4 _TertiaryColor;

			float _BlueMultiplier;

			float _HeatMultiplier;
			
			float _FxState;

			float4 GetEntryColor(float4 heat, float4 blue, float strength)
			{
				float whiteRatio = strength / 0.25;

				float4 col = lerp(heat, blue, strength);

				if (whiteRatio < 2)
				{
					float t = saturate(whiteRatio - 1);

					col = lerp(1, col, t + _FxState);
				}

				return col;
			}

            FS_INPUT vert(VS_INPUT IN)
            {
                FS_INPUT OUT;

				// Position in clip space
                OUT.position = UnityObjectToClipPos(IN.position.xyz);
				
				// World space normal
				OUT.normal = UnityObjectToWorldNormal(IN.normal);

				// Screen POSITION
				OUT.screenPos = ComputeScreenPos(OUT.position);
				OUT.screenPos = OUT.screenPos / OUT.screenPos.w;
				
				// Calculating the clip position on the airstream camera
				OUT.worldPosition = TransformObjectToWorld(IN.position.xyz);
				float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.worldPosition, 1));
				OUT.airstreamNDC = airstreamPosition / airstreamPosition.w;
				
				// View dir
				OUT.viewDir = WorldSpaceViewDir(IN.position);

                return OUT;
            }

            half4 frag(FS_INPUT IN) : SV_Target
            {
				// Occlusion
				float shadow = Shadow(IN.airstreamNDC, -0.002, 0.95);

				// Acquiring the alpha
				float entrySpeed = saturate(GetEntrySpeed() / 4000);
				float whiteRatio = saturate(entrySpeed / 0.42);  // white when below 1680
				float faintRatio = saturate(entrySpeed / 0.5) / 2;

				float rawDot = saturate(dot(IN.normal, _Velocity));
				float velDot = saturate(rawDot + 0.2);

				float alpha = saturate(shadow * whiteRatio * _HeatMultiplier * velDot);
				
				// Entry heat color
				float3 entryHeat = GetEntryColor(_GlowColor, _HotGlowColor, (entrySpeed + _BlueMultiplier)) * shadow * velDot * lerp(0, 4, faintRatio);
				
				// Output
				//clip(DitherTexture(alpha, IN.screenPos.xy));
				return float4(entryHeat, alpha);
				//return float4(shadow, shadow, shadow, 1);
            }
            ENDHLSL
        }
		
		Pass
		{
			Name "Effects Pass"

			Tags { "Queue"="Transparent" "RenderType" = "Transparent" }
			ZWrite Off
			//Blend SrcAlpha OneMinusSrcAlpha
			ZTest LEqual
			Blend SrcAlpha One
			Cull Off
			
			HLSLPROGRAM
			
			#pragma require geometry

			#pragma vertex gs_vert
			#pragma geometry gs_geom
			#pragma fragment gs_frag
			
			float _CameraFov;
			
			float _FxState;
			float _AngleOfAttack;
			
			float _VelDotPower;
			float _ShadowPower;
			float _LengthMultiplier;

			float _TrailAlphaMultiplier;
			float _BlueMultiplier;
			float _SideMultiplier;
			float _OpacityMultiplier;
			float _WrapFresnelModifier;
			float _StreakProbability;
			float _StreakThreshold;

			float4 _SecondaryColor;
			float4 _PrimaryColor;
			float4 _TertiaryColor;
			float4 _LayerColor;
			float4 _LayerStreakColor;

			struct VS_INPUT
			{
				float4 position : POSITION;
				float3 normal : NORMAL;

				float2 uv : TEXCOORD0;
			};

			struct GS_INPUT
			{
				float4 position : POSITION;
				float3 normal : NORMAL;

				float2 uv : TEXCOORD0;

				float3 positionWS : TEXCOORD3;
				float4 airstreamNDC : TEXCOORD4;
				float3 velocityOS : TEXCOORD5;
				float3 normalOS : TEXCOORD6;
				float3 viewDir : TEXCOORD7;
			}; 

			struct GS_DATA
			{
				float4 position : SV_POSITION;
				half4 color : COLOR;

				float3 positionWS : TEXCOORD1;
				float3 positionOS : TEXCOORD2;
				float4 screenPos : TEXCOORD4;
				
				float2 trailPos : TEXCOORD6;
				
				float layer : TEXCOORD7;
				float4 airstreamNDC : TEXCOORD8;
			};

			// Creates a vertex instance that can be added to a triangle stream
			GS_DATA CreateVertex(float3 pos, float layer, float4 airstreamNDC, float trailPosX, float trailPosY, half4 color, half a) 
			{
				GS_DATA o;

				o.position = UnityObjectToClipPos(pos);
				o.color = color;
				o.color.a = a;

				o.positionWS = TransformObjectToWorld(pos);
				o.positionOS = pos;
				o.screenPos = o.position;
				
				o.trailPos = float2(trailPosX, trailPosY);
				
				o.layer = layer;
				
				o.airstreamNDC = airstreamNDC;

				return o;
			}
			
			GS_INPUT gs_vert(VS_INPUT IN)
			{
				GS_INPUT OUT;

				OUT.position = IN.position;
				OUT.normal = UnityObjectToWorldNormal(IN.normal);

				OUT.uv = IN.uv;

				OUT.positionWS = TransformObjectToWorld(IN.position.xyz);

				float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.positionWS, 1));
				OUT.airstreamNDC = float4(airstreamPosition.xyz / airstreamPosition.w, airstreamPosition.w);

				OUT.velocityOS = normalize(UnityWorldToObjectDir(_Velocity));

				OUT.normalOS = normalize(IN.normal);
				
				OUT.viewDir = WorldSpaceViewDir(IN.position);

				return OUT;
			}

			[maxvertexcount(12)]
			void gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
			{
				if (GetEntrySpeed() < 50) return;

				float entrySpeed = GetEntrySpeed() / 4000 - 0.08 * _FxState;
				float whiteRatio = entrySpeed / 0.25;  // white when below 1000
				float transparentRatio = saturate(entrySpeed / 0.025);

				// Get the occlusion for each vertex
				float3 occlusion = float3(
					Shadow(vertex[0].airstreamNDC, -0.0025, 1),
					Shadow(vertex[1].airstreamNDC, -0.0025, 1),
					Shadow(vertex[2].airstreamNDC, -0.0025, 1)
				);
				
				// Calculate the dot product between the inverse normal and airstream velocity
				float3 velDots = 0;
				for (int i = 0; i < 3; i++) velDots[i] = dot(-vertex[i].normal, _Velocity);

				// Calculate the effect length with noise
				float baseLength = GetEntrySpeed() * 0.0013;
				float maxBaseLength = 5.2;  // 4000 * 0.0013

				float3 noise = 0;
				for (int i = 0; i < 3; i++) noise[i] = Noise(vertex[i].position.xy + vertex[i].uv, 1) * baseLength * Noise(vertex[i].position.xy + vertex[i].uv, 1) * 5;

				float3 effectLength = (baseLength + noise) * _LengthMultiplier;
				float3 middleLength = effectLength * 0.2;
				float middleNormalMultiplier = 0.23;
				
				float maxEffectLength = (maxBaseLength * 6) * _LengthMultiplier; // imitating the effectLength variable, noise is at it's maximum, so 1 * maxBaseLength * 1 * 5, which can be simplified to maxBaseLength * 5
				
				// Scale
				effectLength /= _ModelScale.y;
				middleLength /= _ModelScale.y;

				// Iterate through every vertex
				for (uint i = 0; i < 3; i++)
				{
					float velDot = velDots[i];
					
					// DEBUG
					//triStream.Append(CreateVertex(vertex[i].position, 0, 0, velDot, 1));
					// DEBUG

					// Only proceed if the vertex isnt occluded and it's at a high enough angle, creating a rim from which the heat trail is created
					if (velDot > -0.25 && occlusion[i] > 0.9)
					{
						uint j = (i + 1) % 3;  // next vertex
						uint k = (j + 1) % 3;  // another vertex
						
						float edgeLength_j = length(vertex[i].position - vertex[j].position);
						float edgeLength_k = length(vertex[i].position - vertex[k].position);
						float edgeLength = ((edgeLength_j + edgeLength_k) / 2) * _ModelScale.y;
						float edgeMul = clamp(edgeLength / 0.1, 0.1, 10);
						float sideEdgeMul = max(1 - saturate(edgeLength - 0.1), 0.1);
						
						if (occlusion[k] > occlusion[j] || velDots[k] > velDots[j]) j = k;
						
						float3 normalSphere = normalize(vertex[i].position);
						float3 sizeVector = normalize(cross(vertex[i].velocityOS, vertex[i].normalOS));

						float3 side = sizeVector * 0.6 * sideEdgeMul;  // vector pointing to the side, which allows for thicker trails
						float3 middleSide = side * 1.4 * clamp(entrySpeed, 0.2, 1);
						float3 endSide = side * 2.5 * clamp(entrySpeed, 0.2, 1);
						
						float vertNoise = Noise(vertex[i].position.xy + vertex[i].uv + _Time.x, 0);
						float vertNoise1 = Noise(vertex[i].position.xy - _Time.y, 1);
						
						float normalMultiplier = (1.3 + velDot * 1.5) * _LengthMultiplier;

						// Colors
						float4 col = lerp(_PrimaryColor, _SecondaryColor, vertNoise * 0.3);  // color at the start of the trail, primary/secondary
						//col = lerp(col, _TertiaryColor, saturate(entrySpeed - 1.2));
						
						float4 middleCol = lerp(col, _SecondaryColor, 0.5);

						float4 endCol = lerp(_SecondaryColor, _TertiaryColor, clamp(entrySpeed, 0, 1.7));  // color at the end, secondary/tertiary, decided by the entry speed
						//float alpha = (0.007 * _TrailAlphaMultiplier + vertNoise * 0.03);
						float alpha = (0.004 * _TrailAlphaMultiplier + vertNoise * 0.004);

						// Stuff to make the trail more transparent
						side *= 0.3;

						// Mach effects vs reentry Effects
						if (whiteRatio < 2)
						{
							float t = saturate(whiteRatio - 1);
							
							// Actual fx state, if we're going up then it definitely shouldn't be close to 1
							float fxState = lerp(_FxState, _FxState * 0.05, sign(_Velocity.y));

							col = lerp(1, col, saturate(t + _FxState));
							middleCol = lerp(1, middleCol, saturate(t + _FxState));
							endCol = lerp(1, endCol, saturate(t + _FxState));
							
							float aoa = pow(saturate(_AngleOfAttack / 20), 4);
							alpha *= saturate(aoa + t + 0.5);
						}
						
						// Scale
						side /= _ModelScale.x;
						middleSide /= _ModelScale.x;
						endSide /= _ModelScale.x;
						normalMultiplier /= _ModelScale.x;
						
						effectLength = abs(effectLength);
						middleLength = abs(middleLength);
						
						// Make the effect completely transparent at super low speeds
						alpha *= lerp(0, 1, transparentRatio);
						
						// Normal
						float3 trailDir = (vertex[i].position - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed) - vertex[i].position;
						float3 normal = normalize(cross(sizeVector, trailDir));
						float3 viewDir = vertex[i].viewDir;
						
						float vertFresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 2);
						alpha *= saturate(vertFresnel + 0.5 + vertNoise * 0.3);
						
						// Blue streaks
						float streakValue = 0;
						if (vertNoise1 > 0.73 - _StreakProbability && entrySpeed > 0.5 + _StreakThreshold)
						{
							col = _TertiaryColor;
							middleCol = _TertiaryColor;
							endCol = _TertiaryColor;
							effectLength *= 2;
							alpha *= 2;
							
							streakValue = 1;
						}
						
						float3 vertex_b0 = vertex[i].position - side;
						float3 vertex_b1 = vertex[j].position + side;
						
						float3 vertex_m0 = vertex[i].position - middleSide - vertex[i].velocityOS * middleLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
						float3 vertex_m1 = vertex[j].position + middleSide - vertex[j].velocityOS * middleLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
						
						float3 vertex_t0 = vertex[i].position - endSide - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed;
						float3 vertex_t1 = vertex[j].position + endSide - vertex[j].velocityOS * effectLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed;
						
						// Limit length
						float3 m0_ndc = GetAirstreamNDC(lerp(vertex_m0, vertex_m1, 0.5));
						float3 m1_ndc = GetAirstreamNDC(vertex_m1);
						float depth = Shadow(m0_ndc, -0.003, 1);
						float depth1 = Shadow(m1_ndc, -0.003, 1);
						
						if (depth < 0.9 && depth1) return;
						
						// Alpha scale
						alpha *= edgeMul;

						// Bottom Edge
						triStream.Append(CreateVertex(vertex_b0, 0, vertex[i].airstreamNDC, 0, 0, col, alpha));
						triStream.Append(CreateVertex(vertex_b1, 0, vertex[j].airstreamNDC, 1, 0, col, alpha));
						
						// Middle Edge
						triStream.Append(CreateVertex(vertex_m0, 0, vertex[i].airstreamNDC, 0, 0.5, middleCol, alpha));
						triStream.Append(CreateVertex(vertex_m1, 0, vertex[j].airstreamNDC, 1, 0.5, middleCol, alpha));
						
						// Top edge
						triStream.Append(CreateVertex(vertex_t0, 0, vertex[i].airstreamNDC, 0, 1, endCol, 0));
						triStream.Append(CreateVertex(vertex_t1, 0, vertex[j].airstreamNDC, 1, 1, endCol, 0));

						// Restart the strip
						triStream.RestartStrip();
						
						// 
						// SECOND LAYER
						// 
						
						//return;
						if (_FxState < 0.6) return;
						
						//entrySpeed = lerp(0, entrySpeed + 0.4, saturate((entrySpeed - 0.2) * 2));
						entrySpeed = clamp(entrySpeed, 0, 1);
						
						vertFresnel = Fresnel(vertex[i].normal, vertex[i].viewDir, 1);
						vertFresnel += (1 - vertFresnel) * _WrapFresnelModifier;
						
						middleLength = clamp(middleLength, 0, maxEffectLength * 0.2);
						effectLength = clamp(effectLength * 3.4 * clamp(entrySpeed, 0, 0.6) * _FxState, 0, maxEffectLength * 1.6);
						alpha *= 1 * vertFresnel * min(entrySpeed, 0.7) * _FxState;
						normalMultiplier = -2.325 * _LengthMultiplier / _ModelScale.x;
						middleNormalMultiplier = 0.05;
						col = _PrimaryColor;
						middleCol = lerp(_LayerColor, _LayerStreakColor, streakValue);
						endCol = lerp(lerp(_LayerColor, _SecondaryColor, 0.5), _LayerStreakColor, streakValue);
						float3 layerOffset = vertex[i].velocityOS * -0.05 * _LengthMultiplier / _ModelScale.y;
						
						vertex_b0 = vertex[i].position - side + layerOffset;
						vertex_b1 = vertex[j].position + side + layerOffset;
						
						vertex_m0 = vertex[i].position - middleSide + layerOffset - vertex[i].velocityOS * middleLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
						vertex_m1 = vertex[j].position + middleSide + layerOffset - vertex[j].velocityOS * middleLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed * middleNormalMultiplier;
						
						vertex_t0 = vertex[i].position - endSide + layerOffset - vertex[i].velocityOS * effectLength[i] + vertex[i].normalOS * normalMultiplier * entrySpeed;
						vertex_t1 = vertex[j].position + endSide + layerOffset - vertex[j].velocityOS * effectLength[j] + vertex[j].normalOS * normalMultiplier * entrySpeed;

						// Bottom Edge
						triStream.Append(CreateVertex(vertex_b0, 1, vertex[i].airstreamNDC, 0, 0, col, alpha));
						triStream.Append(CreateVertex(vertex_b1, 1, vertex[j].airstreamNDC, 1, 0, col, alpha));
						
						// Middle Edge
						triStream.Append(CreateVertex(vertex_m0, 1, vertex[i].airstreamNDC, 0, 0.5, middleCol, alpha));
						triStream.Append(CreateVertex(vertex_m1, 1, vertex[j].airstreamNDC, 1, 0.5, middleCol, alpha));
						
						// Top edge
						triStream.Append(CreateVertex(vertex_t0, 1, vertex[i].airstreamNDC, 0, 1, endCol, 0));
						triStream.Append(CreateVertex(vertex_t1, 1, vertex[j].airstreamNDC, 1, 1, endCol, 0));
						
						// Restart the strip
						triStream.RestartStrip();
					}
				}

				// DEBUG	
				//triStream.RestartStrip();
				// DEBUG
			}

			half4 gs_frag(GS_DATA IN) : SV_Target
			{
				float4 c = IN.color;
				
				float entrySpeed = GetEntrySpeed() / 4000 - 0.08 * _FxState;
				float speedScalar = saturate(lerp(0, 2.5, entrySpeed));
				
				// angle
				float2 circleCoord = GetAirstreamNDC(normalize(IN.positionOS));
				float2 circleCoord2 = normalize(IN.positionOS.xz);
				float angle = atan2(circleCoord.y, circleCoord.x);
				float angle2 = atan2(circleCoord2.y, circleCoord2.x);
				
				// position along the trail, used to scale the noise
				float2 trailPos = 1 - IN.trailPos;
				float trailPosScalar0 = pow(trailPos.y, 2);
				float trailPosScalar1 = 0.2;
				float trailPosScalar = lerp(trailPosScalar0, trailPosScalar1, IN.layer);
				float invTrailPosScalar = 1 - trailPosScalar;
				
				// calculate the uv
				float2 scrollScale = float2(lerp(0.6, 0.1, IN.layer), lerp(-8, -0.2, IN.layer));
				float2 timeOffset = float2(_Time.y * scrollScale.x, _Time.y * scrollScale.y * (entrySpeed + 0.5));
				float2 scale0 = float2(0.1, 2);
				float2 scale1 = float2(lerp(1, 0.2, trailPosScalar0 + 0.5), lerp(1, 0.1, trailPosScalar0 + 0.5));
				float2 uv = lerp(scale0, scale1, IN.layer) * trailPos + float2(angle, 0) - timeOffset;
				
				// sample the noise, and scale it with the entry speed and the position along the trail
				float noise = NoiseStatic(uv, lerp(3, 2, IN.layer)) * speedScalar * trailPosScalar;
				float noiseSign = lerp(1, -1, IN.layer);
				
				// clamp
				noise = max(0, noise);
				
				// define the alpha for both layers
				float alpha0 = saturate(c.a + noise * 0.05);
				float alpha1 = saturate(c.a - (noise * c.a * 7));
				
				// define the scalar for both layers
				float scalar0 = (0.1 + invTrailPosScalar * 0.05);
				float scalar1 = 1 - trailPosScalar0;
				
				// modify the output alpha by the noise
				c.a = lerp(alpha0, alpha1, IN.layer) * lerp(scalar0, scalar1, IN.layer) * _OpacityMultiplier;
				
				//return float4(circleCoord2.x, 0, 0, 1);
				//return c * IN.layer;
				//return noise * 0.5 * IN.layer;
				return c;
			}
			
			ENDHLSL
		}
		
		Pass
		{
			Name "Bowshock Pass"
			
			ZWrite Off
			Cull Off
			Blend SrcAlpha One
			ZTest LEqual
			
			HLSLPROGRAM
			
			#pragma require geometry

			#pragma vertex gs_vert
			#pragma geometry gs_geom
			#pragma fragment gs_frag
			
			float4 _ShockwaveColor;
			float _LengthMultiplier;

			struct VS_INPUT
			{
				float4 position : POSITION;
				float3 normal : NORMAL;

				float2 uv : TEXCOORD0;
			};

			struct GS_INPUT
			{
				float4 position : POSITION;
				float3 normal : NORMAL;

				float2 uv : TEXCOORD0;

				float3 positionWS : TEXCOORD3;
				float3 airstreamNDC : TEXCOORD4;
				float3 velocityOS : TEXCOORD5;
				float3 normalOS : TEXCOORD6;
				float3 viewDir : TEXCOORD7;
			}; 

			struct GS_DATA
			{
				float4 position : SV_POSITION;
				half4 color : COLOR;

				float3 positionWS : TEXCOORD1;
				float4 screenPos : TEXCOORD3;
			};
			
			// Creates a vertex instance that can be added to a triangle stream
			GS_DATA CreateVertex(float3 pos, half4 color, half a) 
			{
				GS_DATA o;

				o.position = UnityObjectToClipPos(pos);
				o.color = color;
				o.color.a = a;

				o.positionWS = TransformObjectToWorld(pos);

				o.screenPos = ComputeGrabScreenPos(o.position);

				return o;
			}

            GS_INPUT gs_vert(VS_INPUT IN)
			{
				GS_INPUT OUT;

				OUT.position = IN.position;
				OUT.normal = normalize(UnityObjectToWorldNormal(IN.normal));

				OUT.uv = IN.uv;

				OUT.positionWS = TransformObjectToWorld(IN.position.xyz);

				float4 airstreamPosition = mul(_AirstreamVP, float4(OUT.positionWS, 1));
				OUT.airstreamNDC = airstreamPosition.xyz / airstreamPosition.w;

				OUT.velocityOS = UnityWorldToObjectDir(normalize(_Velocity));

				OUT.normalOS = normalize(IN.normal);
				
				OUT.viewDir = WorldSpaceViewDir(IN.position);

				return OUT;
			}

			[maxvertexcount(9)]
			void gs_geom(triangle GS_INPUT vertex[3], inout TriangleStream<GS_DATA> triStream)
			{
				if (GetEntrySpeed() < 1000) return;
				
				// Initialize iterator variables
				int i = 0;
				
				// Scale the entry speed
				float clampedEntrySpeed = min(GetEntrySpeed(), 2300);
				float entrySpeed = min(GetEntrySpeed() / 4000, 0.57);
				float scaledEntrySpeed = lerp(0, entrySpeed + 0.55, saturate((entrySpeed - 0.32) * 2));

				// Get the occlusion for each vertex
				float3 occlusion = float3(
					Shadow(vertex[0].airstreamNDC, -0.002, 1),
					Shadow(vertex[1].airstreamNDC, -0.002, 1),
					Shadow(vertex[2].airstreamNDC, -0.002, 1)
				);
				
				// Calculate the effect length
				float baseLength = clampedEntrySpeed * 0.0005;
				
				// Sample noise
				float3 noise = 0;
				for (i = 0; i < 3; i++) noise[i] = Noise(vertex[i].position.xy + vertex[i].uv, 1) * baseLength * Noise(vertex[i].position.xy + vertex[i].uv, 2) * 10;
				
				// Calculate the effect length going sideways
				float3 effectLength = (baseLength + noise * 0.3) * scaledEntrySpeed * 1.5;
				float3 middleLength = effectLength * 0.5;
				
				// Calculate the effect length going with the velocity
				float3 effectSideLength = (3 + noise) * scaledEntrySpeed * 0.9;
				float3 middleSideLength = effectSideLength * 0.4;
				
				// Colors
				float4 startCol = _ShockwaveColor;
				float4 middleCol = startCol * 1;
				float4 endCol = startCol * 1;
				float4 bowlCol = startCol * 1;
				
				// Offset
				float3 offset = (vertex[0].velocityOS * 0.4 * entrySpeed) / _ModelScale.y;
				float3 trailOffset = offset * 1.05;
				
				// Fresnel effect
				float3 vertFresnel = 0;
				for (i = 0; i < 3; i++) vertFresnel[i] = Fresnel(vertex[i].normal, vertex[i].viewDir, 1) * Fresnel(-vertex[i].normal, vertex[i].viewDir, 1) - (Fresnel(vertex[i].normal, vertex[i].viewDir, 3) - 0.3);
				
				// Dot product of normal and velocity
				float3 velDotInv = 0;
				for (i = 0; i < 3; i++) velDotInv[i] = dot(vertex[i].normal, _Velocity);
				
				// Inverted dot product of normal and velocity	
				float3 velDot = 0;
				for (i = 0; i < 3; i++) velDot[i] = dot(-vertex[i].normal, _Velocity);
				
				// Create the "bowl"
				if (occlusion[0] > 0.9 && velDotInv[0] > 0.2)
				{
					for (i = 0; i < 3; i++)
					{
						float fresnel = saturate(pow(vertFresnel[i], 1));
						float viewVelDot = saturate(saturate(dot(_Velocity, vertex[i].viewDir)) + 0.7);
						
						float alpha = 0.32 * fresnel * viewVelDot * scaledEntrySpeed;
					
						triStream.Append(CreateVertex(vertex[i].position + offset, bowlCol * (velDotInv[i] + 0.6), alpha));
						//triStream.Append(CreateVertex(vertex[i].position + offset, fresnel, 1));
					}
					
					triStream.RestartStrip();
				}
				
				// Scale
				effectLength /= _ModelScale.y;
				middleLength /= _ModelScale.y;
				middleSideLength /= _ModelScale.y;
				effectSideLength /= _ModelScale.y;
				
				effectLength = abs(effectLength);
				middleLength = abs(middleLength);
				middleSideLength = abs(middleSideLength);
				effectSideLength = abs(effectSideLength);

				// Iterate through every vertex
				for (uint i = 0; i < 3; i++)
				{
					if (occlusion[i] > 0.9 && velDot[i] > -0.4 && velDot[i] < 0 && pow(vertFresnel[i], 2) > 0.2)
					{
						uint j = (i + 1) % 3;  // next vertex
						uint k = (j + 1) % 3;  // another vertex
						
						// Get the average edge length
						float edgeLength_j = length(vertex[i].position - vertex[j].position);
						float edgeLength_k = length(vertex[i].position - vertex[k].position);
						float edgeLength = ((edgeLength_j + edgeLength_k) / 2) * _ModelScale.y;
						float edgeMul = clamp(edgeLength / 0.1, 0.1, 1);
						
						// Create the offsets which move the trail a bit inside of the bowl
						float3 offset_i = float3(vertex[i].position.x, 0, vertex[i].position.z) * 0.0;
						float3 offset_j = float3(vertex[j].position.x, 0, vertex[j].position.z) * 0.0;
						
						// Sample noise
						float vertNoise = Noise(vertex[i].position.xy + vertex[i].uv + _Time.x, 0);
						
						// Create the vector which will be used to widen the trail segments
						float3 sizeVector = -normalize(cross(vertex[i].velocityOS, vertex[i].normalOS));
						
						// Width
						float3 side = sizeVector * 0.2;  // vector pointing to the side, which allows for thicker trails
						float3 middleSide = side * 2;
						float3 endSide = side * 2;
						
						// Scale
						side /= _ModelScale.x;
						middleSide /= _ModelScale.x;
						endSide /= _ModelScale.x;
						
						// Alpha
						//float alpha = (0.02 + vertNoise * 0.009) * saturate(vertFresnel[i] + 0.35) * 0.35 * scaledEntrySpeed;
						float alpha = (0.08 + vertNoise * 0.009) * saturate(pow(vertFresnel[i], 2)) * 0.5 * scaledEntrySpeed;
						float middleAlpha = alpha * 1;
						alpha *= edgeMul;
						middleAlpha *= edgeMul;
						
						// Define the vertex positions
						float3 vertex_b0 = vertex[i].position - offset_i + trailOffset - side;
						float3 vertex_b1 = vertex[j].position - offset_j + trailOffset + side;
						
						float3 vertex_m0 = vertex[i].position - offset_i + trailOffset - middleSide + middleLength[i] * vertex[i].normalOS - vertex[i].velocityOS * middleSideLength[i];
						float3 vertex_m1 = vertex[j].position - offset_j + trailOffset + middleSide + middleLength[j] * vertex[j].normalOS - vertex[j].velocityOS * middleSideLength[j];
						
						float3 vertex_t0 = vertex[i].position - offset_i + trailOffset - endSide + effectLength[i] * vertex[i].normalOS - vertex[i].velocityOS * effectSideLength[i];
						float3 vertex_t1 = vertex[j].position - offset_j + trailOffset + endSide + effectLength[j] * vertex[j].normalOS - vertex[j].velocityOS * effectSideLength[j];
						
						// Bottom edge
						triStream.Append(CreateVertex(vertex_b0, startCol, alpha));
						triStream.Append(CreateVertex(vertex_b1, startCol, alpha));
						
						// Middle edge
						triStream.Append(CreateVertex(vertex_m0, middleCol, middleAlpha));
						triStream.Append(CreateVertex(vertex_m1, middleCol, middleAlpha));
						
						// Top edge
						triStream.Append(CreateVertex(vertex_t0, endCol, 0));
						triStream.Append(CreateVertex(vertex_t1, endCol, 0));
						
						// Restart the strip
						triStream.RestartStrip();
					}
				}
			}

			half4 gs_frag(GS_DATA IN) : SV_Target
			{
				float4 c = IN.color;
				
				return c;
			}
			
			ENDHLSL
		}
    }
}