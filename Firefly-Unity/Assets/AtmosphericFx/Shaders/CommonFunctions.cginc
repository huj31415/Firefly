Texture2D _AirstreamTex;
SamplerState sampler_AirstreamTex;

Texture2D _NoiseTex;
SamplerState sampler_NoiseTex;

Texture2D _DitherTex;
SamplerState sampler_DitherTex;

float _EntrySpeed;
float _EntrySpeedMultiplier;

float3 _ModelScale;
float3 _EnvelopeScaleFactor;
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
	return _NoiseTex.SampleLevel(sampler_NoiseTex, uv + float2(_Time.x*14, _Time.x*7), 0)[channel];
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