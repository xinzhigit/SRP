#ifndef SRP_LIT_INCLUDE
#define SRP_LIT_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

#include "Lighting.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
	float4x4 unity_WorldToObject;
	float4 unity_LightIndicesOffsetAndCount;
	float4 unity_4LightIndices0, unity_4LightIndices1;
	float4 unity_ProbesOcclusion;
	float4 unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax;
	float4 unity_SpecCube0_ProbePosition, unity_SpecCube0_HDR;
	float4 unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax;
	float4 unity_SpecCube1_ProbePosition, unity_SpecCube1_HDR;
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;
	float4 unity_SHAr, unity_SHAg, unity_SHAb;
	float4 unity_SHBr, unity_SHBg, unity_SHBb;
	float4 unity_SHC;
CBUFFER_END

CBUFFER_START(UnityPerCamera)
		float3 _WorldSpaceCameraPos;
CBUFFER_END

#define MAX_VISIBLE_LIGHTS 16
CBUFFER_START(_LightBuffer)
	float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
	float4 _visibleLightDirectionsOrPositions[MAX_VISIBLE_LIGHTS];
	float4 _visibleLightAttenuations[MAX_VISIBLE_LIGHTS];
	float4 _visibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
	float4 _visibleLightOcclusionMasks[MAX_VISIBLE_LIGHTS];
CBUFFER_END

CBUFFER_START(_ShadowBuffer)
	float4x4 _WorldToShadowMatrices[MAX_VISIBLE_LIGHTS];
	float4 _ShadowDatas[MAX_VISIBLE_LIGHTS];
	float4 _ShadowMapSize;
	float4 _GlobalShadowData;
	float4 _CascadedShadowMapSize;
	float _CascadedShadowStrength;
	float4 _CascadedCullingSpheres[4];
	float4x4 _CascadedWorldToShadowMatrices[5];
	float4 _SubtractiveShadowColor;
CBUFFER_END

CBUFFER_START(UnityProbeVolume)
	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float3 unity_ProbeVolumeSizeInv;
	float3 unity_ProbeVolumeMin;
CBUFFER_END

TEXTURE3D_FLOAT(unity_ProbeVolumeSH);
SAMPLER(samplerunity_ProbeVolumeSH);

TEXTURE2D_SHADOW(_ShadowMap);
SAMPLER_CMP(sampler_ShadowMap);

TEXTURE2D_SHADOW(_CascadedShadowMap);
SAMPLER_CMP(sampler_CascadedShadowMap);

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

TEXTURE2D(unity_Lightmap);
SAMPLER(samplerunity_Lightmap);
TEXTURE2D(unity_DynamicLightmap);
SAMPLER(samplerunity_DynamicLightmap);
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	float _Cutoff;
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject

#if !defined(LIGHTMAP_ON)
	#if defined(_SHADOWMASK) || defined(_DISTANCE_SHADOWMASK) || defined(_SUBTRACTIVE_LIGHTING)
		#define SHADOWS_SHADOWMASK
	#endif
#endif
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Emission)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(PerInstance)

//float DistanceToCameraSqrt(float3 worldPos) {
//	float3 cameraToFragment = worldPos - _WorldSpaceCameraPos;
//	return dot(cameraToFragment, cameraToFragment);
//}

float RealtimeToBakedShadowsInterpolator(float3 worldPos) {
	float d = distance(worldPos, _WorldSpaceCameraPos);
	return saturate(d * _GlobalShadowData.z + _GlobalShadowData.w);
}

float MixRealtimeAndBakedShadowAttenuation(float realtime, float4 bakedShadows, int lightIndex, float3 worldPos, bool mainLight = false) {
	float t = RealtimeToBakedShadowsInterpolator(worldPos);
	float fadeRealtime = saturate(realtime + t);
	float4 occlusionMask = _visibleLightOcclusionMasks[lightIndex];
	float baked = dot(bakedShadows, occlusionMask);

#if defined(_SHADOWMASK)
	if (occlusionMask.x >= 0.0) {
		return min(fadeRealtime, baked);
	}
#elif defined(_DISTANCE_SHADOWMASK)
	if (occlusionMask.x >= 0.0) {
		// point light
		if (!mainLight && _visibleLightSpotDirections[lightIndex].w > 0.0) {
			return baked;
		}
		return lerp(fadeRealtime, baked, t);
	}
#elif defined(_SUBTRACTIVE_LIGHTING)
	// if we have subtractive lighting and are working on dynamic objects,
	// then we must mix shadows like the regular shadowmask mode, but only for the main light.
	// Thus, we always use the first channel of the baked shadows.
	#if !defined(LIGHTMAP_ON)
		if (mainLight) {
			return min(fadeRealtime, bakedShadows.x);
		}
	#endif
	#if !defined(_CASCADED_SHADOWS_HARD) || !defined(_CASCADED_SHADOWS_SOFT)
		if (lightIndex == 0) {
			return bakedShadows.x;
		}
	#endif
#endif
	return fadeRealtime;
}

bool SkipRealtimeShadows(float3 worldPos) {
	return RealtimeToBakedShadowsInterpolator(worldPos) > 1.0;
}

float InsideCascadedCullingSphere(int index, float3 worldPos) {
	float4 sphere = _CascadedCullingSpheres[index];
	float3 dir = worldPos - sphere.xyz;
	return dot(dir, dir) < sphere.w;
}

float3 BoxProjection(float3 direction, float3 position, float4 cubemapPosition, float4 boxMin, float4 boxMax) {
	UNITY_BRANCH
		if (cubemapPosition.w > 0) {
			float3 factors = ((direction > 0 ? boxMax.xyz : boxMin.xyz) - position) / direction;
			float scalar = min(min(factors.x, factors.y), factors.z);
			direction = direction * scalar + (position - cubemapPosition.xyz);
		}
	return direction;
}

float3 SampleEnvironment(SurfaceData s) {
	float3 reflectVector = reflect(-s.viewDir, s.normal);
	float mip = PerceptualRoughnessToMipmapLevel(s.perceptualRoughness);

	float4 cube0Simple = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, sampleruity_SpecCube0, reflectVector, mip);
	float3 color = DecodeHDREnvironment(cube0Simple, unity_SpecCube0_HDR);

	// box projection
	//float3 uvw = BoxProjection(reflectVector, s.worldPos, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
	//float4 cube0Simple = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, sampleruity_SpecCube0, uvw, mip);
	//float3 color = DecodeHDREnvironment(cube0Simple, unity_SpecCube0_HDR);

	// reflect probe blend
	//float blend = unity_SpecCube0_BoxMin.w;
	//if (blend < 0.99999) {
	//	uvw = BoxProjection(reflectVector, s.worldPos, unity_SpecCube1_ProbePosition, unity_SpecCube1_BoxMin, unity_SpecCube1_BoxMax);
	//	float4 cube1Simple = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube1, sampleruity_SpecCube0, uvw, mip);
	//	float3 cube1Color = DecodeHDREnvironment(cube1Simple, unity_SpecCube1_HDR);
	//	color = lerp(cube1Color, color, blend);
	//}

	return color;
}

float3 SampleLightmap(float2 uv) {
	return SampleSingleLightmap(
		TEXTURE2D_ARGS(unity_Lightmap, samplerunity_Lightmap), uv,
		float4(1, 1, 0, 0),
		#if defined(UNITY_LIGHTMAP_FULL_HDR)
			false,
		#else
			true,
		#endif
		float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
	);
}

float3 SampleDynamicLightmap(float2 uv) {
	return SampleSingleLightmap(
		TEXTURE2D_ARGS(unity_DynamicLightmap, samplerunity_DynamicLightmap), uv,
		float4(1, 1, 0, 0),
		false,
		float4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0, 0.0)
	);
}

float3 SampleLightProbes(SurfaceData s) {
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeVolumeSH4(
			TEXTURE2D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			s.worldPos, s.normal, unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin, unity_ProbeVolumeSizeInv
		);
	}
	else {
		float4 coefficents[7];
		coefficents[0] = unity_SHAr;
		coefficents[1] = unity_SHAg;
		coefficents[2] = unity_SHAb;
		coefficents[3] = unity_SHBr;
		coefficents[4] = unity_SHBg;
		coefficents[5] = unity_SHBb;
		coefficents[6] = unity_SHC;
		return max(0.0, SampleSH9(coefficents, s.normal));
	}
}

float HardShadowAttenuation(float4 shadowPos, bool cascade = false) {
	if (cascade) {
		return SAMPLE_TEXTURE2D_SHADOW(_CascadedShadowMap, sampler_CascadedShadowMap, shadowPos.xyz);
	}
	else {
		return SAMPLE_TEXTURE2D_SHADOW(_ShadowMap, sampler_ShadowMap, shadowPos.xyz);
	}
}

float SoftShadowAttenuation(float4 shadowPos, bool cascade = false) {
	real tentWeights[9];
	real2 tentUVs[9];
	float4 size = cascade ? _CascadedShadowMapSize : _ShadowMapSize;
	SampleShadow_ComputeSamples_Tent_5x5(size, shadowPos.xyz, tentWeights, tentUVs);

	float attenuation = 0;
	for (int n = 0; n < 9; ++n) {
		attenuation += tentWeights[n] * HardShadowAttenuation(float4(tentUVs[n].xy, shadowPos.z, 0), cascade);
	}

	return attenuation;
}

float ShadowAttenuation(int index, float3 worldPos) {
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#elif !defined(_SHADOWS_HARD) && !defined(_SHADOWS_SOFT)
	return 1.0;
#endif
	float strength = _ShadowDatas[index].x;
	//if (strength <= 0 || (DistanceToCameraSqrt(worldPos) > _GlobalShadowData.y)) {
	if (strength <= 0 || SkipRealtimeShadows(worldPos)) {
		return 1.0;
	}

    float4 shadowPos = mul(_WorldToShadowMatrices[index], float4(worldPos, 1.0f));
    shadowPos.xyz /= shadowPos.w;

	// clamp the XY coordinates of the shadow position after the perspective division. After that, apply the tile transformation.
	shadowPos.xy = saturate(shadowPos.xy);
	shadowPos.xy = shadowPos.xy * _GlobalShadowData.x + _ShadowDatas[index].zw;

	float attenuation = 0;
#if defined(_SHADOWS_HARD)
	#if defined(_SHADOWS_SOFT)
		if (_ShadowDatas[index].y == 0) {
			attenuation = HardShadowAttenuation(shadowPos);
		}
		else {
			attenuation = SoftShadowAttenuation(shadowPos);
		}
	#else
		attenuation = HardShadowAttenuation(shadowPos);
	#endif
#else
	attenuation = SoftShadowAttenuation(shadowPos);
#endif

	return lerp(1, attenuation, strength);
}

float CascadedShadowAttenuation(float3 worldPos, bool applyStrength = true) {
#if !defined(_RECEIVE_SHADOWS)
	return 1.0;
#elif !defined(_CASCADED_SHADOWS_HARD) && !defined(_CASCADED_SHADOWS_SOFT)
	return 1.0;
#endif

	//if (DistanceToCameraSqrt(worldPos) > _GlobalShadowData.y) {
	if (SkipRealtimeShadows(worldPos)) {
		return 1.0;
	}

	float4 cascadeFlags = float4(
		InsideCascadedCullingSphere(0, worldPos),
		InsideCascadedCullingSphere(1, worldPos),
		InsideCascadedCullingSphere(2, worldPos),
		InsideCascadedCullingSphere(3, worldPos));

	// If a point lies within a sphere, then it also lies inside all larger spheres.
	// So we can end up with five different flag configurations: (1,1,1,1), (0,1,1,1), (0,0,1,1), (0,0,0,1), or (0,0,0,0).
	// We can use this to visualize the cascades, by summing the flags and dividing by four.
	// That can be done by taking the dot product of the flags and (¼,¼,¼,¼).
	//return dot(cascadeFlags, 0.25);

	// We want to use the first cascade that is valid,
	// so we have to clear all the flags after the first one that's set.
	// The first flag is always good, but the second should be cleared if the first one is set.
	// And the third should be cleared when the second is set; likewise for the fourth.
	// We can do that by subtracting the XYZ components from YZW and saturating the result.
	// If we take the dot product of that result with (0,1,2,3), then we end up with the final cascade index. The conversion goes like this:
	// (1, 1, 1, 1) →(1, 0, 0, 0) → 0
	// (0, 1, 1, 1) →(0, 1, 0, 0) → 1
	// (0, 0, 1, 1) →(0, 0, 1, 0) → 2
	// (0, 0, 0, 1) →(0, 0, 0, 1) → 3
	// (0, 0, 0, 0) →(0, 0, 0, 0) → 0
	cascadeFlags.yzw = saturate(cascadeFlags.yzw - cascadeFlags.xyz);
	//float cascadeIndex = dot(cascadeFlags, float4(0, 1, 2, 3));

	// Now we have to include the(0, 0, 0, 0) → 4 conversion, which we can do by starting with 4 and subtracting the dot product of the isolated flag with(4, 3, 2, 1).
	float cascadeIndex = 4 - dot(cascadeFlags, float4(4, 3, 2, 1));
	float4 shadowPos = mul(_CascadedWorldToShadowMatrices[cascadeIndex], float4(worldPos, 1));

	float attenuation = 0;
#if defined(_CASCADED_SHADOWS_HARD)
	attenuation = HardShadowAttenuation(shadowPos, true);
#else
	attenuation = SoftShadowAttenuation(shadowPos, true);
#endif

	if (applyStrength) {
		return lerp(1, attenuation, _CascadedShadowStrength);
	}
	else {
		return attenuation;
	}
}

// We can support both with the same calculation, by multiplying the world position with the W component of the light's direction or position vector.
// If it's a position vector, then W is 1 and the calculation is unchanged.
// But if it's a direction vector, then W is 0 and the subtraction is eliminated.
// So we end up normalizing the original direction vector, which makes no difference.
float3 GenericLight(int index, SurfaceData s, float shadowAttenuation) {
	float3 lightColor = _VisibleLightColors[index].rgb;
	float4 lightPositionOrDirection = _visibleLightDirectionsOrPositions[index];
	float4 lightAttenuation = _visibleLightAttenuations[index];
	float3 spotDir = _visibleLightSpotDirections[index];

	float3 lightDir = normalize(lightPositionOrDirection.xyz - s.worldPos * lightPositionOrDirection.w);
	float3 color = LightSurface(s, lightDir);

	float distSqrt = dot(lightDir, lightDir);
	float rangeFade = distSqrt * lightAttenuation.x;
	rangeFade = saturate(1.0 - rangeFade * rangeFade);
	rangeFade *= rangeFade;

	float spotFade = dot(spotDir, lightDir);
	spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
	spotFade *= spotFade;

	distSqrt = max(distSqrt, 0.00001);
	color *= spotFade * rangeFade / distSqrt * shadowAttenuation;
	color *= lightColor;

	return color;
}

float3 MainLight(SurfaceData s, float shadowAttenuation) {
	float3 lightColor = _VisibleLightColors[0].rgb;
	float3 lightDir = _visibleLightDirectionsOrPositions[0].xyz;

	float3 color = LightSurface(s, lightDir);
	color *= shadowAttenuation;
	return color * lightColor;
}

float3 SubtractiveLighting(SurfaceData s, float3 bakedLighting) {
	float3 lightColor = _VisibleLightColors[0].rgb;
	float3 lightDir = _visibleLightDirectionsOrPositions[0].xyz;
	float3 diffuse = lightColor * saturate(dot(lightDir, s.normal));
	float shadowAttenuation = saturate(CascadedShadowAttenuation(s.worldPos, false) + RealtimeToBakedShadowsInterpolator(s.worldPos));
	float3 shadowLightingGuess = diffuse * (1.0 - shadowAttenuation);
	float3 substractedLighting = bakedLighting - shadowLightingGuess;
	substractedLighting = max(substractedLighting, _SubtractiveShadowColor);
	substractedLighting = lerp(bakedLighting, substractedLighting, _CascadedShadowStrength);
	return min(bakedLighting, substractedLighting);
}

struct VertexInput {
	float4 pos : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	float2 lightmapUV : TEXCOORD1;
	float3 dynamicLightmapUV : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	float3 normal : TEXCOORD0;
	float3 worldPos : TEXCOORD1;
	float3 vertexLighting : TEXCOORD2;
	float2 uv : TEXCOORD3;
#if defined(LIGHTMAP_ON)
	float2 lightmapUV : TEXCOORD4;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
	float2 dynamicLightmapUV : TEXCOORD5;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

// In GlobalIllumination, sample the dynamic light map if it is available.
// Both baked and realtime light maps can be used at the same time, so add them in that case.
float3 GlobalIllumination(VertexOutput i, SurfaceData s) {
#if defined(LIGHTMAP_ON)
	float3 gi = SampleLightmap(i.lightmapUV);
	#if defined(_SUBTRACTIVE_LIGHTING)
		gi = SubtractiveLighting(s, gi);
	#endif
	#if defined(DYNAMICLIGHTMAP_ON)
		gi += SampleDynamicLightmap(i.dynamicLightmapUV);
	#endif
	return gi;
#elif defined(DYNAMICLIGHTMAP_ON)
	return SampleDynamicLightmap(i.dynamicLightmapUV);
#else
	return SampleLightProbes(s);
#endif
}

float4 BakedShadows(VertexOutput i, SurfaceData s) {
#if defined(LIGHTMAP_ON)
	#if defined(_SHADOWMASK) || defined(_DISTANCE_SHADOWMASK)
		return SAMPLE_TEXTURE2D(unity_ShadowMask, samplerunity_ShadowMask, i.lightmapUV);
	#endif
#elif defined(_SHADOWMASK) || defined(_DISTANCE_SHADOWMASK) || defined(_SUBTRACTIVE_LIGHTING)
	if (unity_ProbeVolumeParams.x) {
		return SampleProbeOcclusion(
			TEXTURE3D_ARGS(unity_ProbeVolumeSH, samplerunity_ProbeVolumeSH),
			s.worldPos, unity_ProbeVolumeWorldToObject,
			unity_ProbeVolumeParams.y, unity_ProbeVolumeParams.z,
			unity_ProbeVolumeMin, unity_ProbeVolumeSizeInv
		);
	}
	return unity_ProbesOcclusion;
#endif
	return 1.0;
}

VertexOutput LitPassVertex(VertexInput i) {
    VertexOutput o;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);

	// By making float4(i.pos.xyz, 1.0) explicit we make it possible for the compiler to optimize the computation.
    float4 worldPos = mul(UNITY_MATRIX_M, float4(i.pos.xyz, 1.0));
    o.worldPos = worldPos;
    o.clipPos = mul(unity_MatrixVP, worldPos);

#if defined(UNITY_ASSUME_UNIFORM_SCALING)
    o.normal = mul((float3x3)UNITY_MATRIX_M, i.normal);
#else
	o.normal = mul(i.normal, (float3x3)UNITY_MATRIX_I_M);
#endif

	o.uv = TRANSFORM_TEX(i.uv, _MainTex);
#if defined(LIGHTMAP_ON)
	o.lightmapUV = i.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
#endif
#if defined(DYNAMICLIGHTMAP_ON)
	o.dynamicLightmapUV = i.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

	SurfaceData s = GetSurfaceDataVertex(o.normal, o.worldPos);

	// vertex lights
    o.vertexLighting = 0;
    for (int n = 4; n < min(unity_LightIndicesOffsetAndCount.y, 8); ++n)
    {
        int lightIndex = unity_4LightIndices1[n - 4];
		o.vertexLighting += GenericLight(lightIndex, s, 1);
	}

    return o;
}

float4 LitPassFragment(VertexOutput i, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_TARGET {
    UNITY_SETUP_INSTANCE_ID(i);

	float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
    albedo *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);

#if defined(_CLIPPING_ON)
	clip(albedo.a - _Cutoff);
#endif

    float3 normal = normalize(i.normal);
	normal = IS_FRONT_VFACE(isFrontFace, normal, -normal);

	float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
	float metallic = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Metallic);
	float smoothness = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Smoothness);
	SurfaceData s = GetSurfaceData(normal, i.worldPos, viewDir, albedo.rgb, metallic, smoothness);

#if defined(_PREMULTIPLY_ALPHA)
	PremultiplyAlpha(s, albedo.a);
#endif

	float4 bakedShadows = BakedShadows(i, s);

	// pixel lights
	float shadowAttenuation = 0;
	float mixedAttenuation = 0;
    float3 color = i.vertexLighting * s.albedo;
#if defined(_CASCADED_SHADOWS_HARD) || defined(_CASCADED_SHADOWS_SOFT)
	#if !(defined(LIGHTMAP_ON) && defined(_SUBTRACTIVE_LIGHTING))
		shadowAttenuation = CascadedShadowAttenuation(s.worldPos)
		mixedAttenuation = MixRealtimeAndBakedShadowAttenuation(shadowAttenuation, bakedShadows, 0, s.worldPos, true);
		color += MainLight(s, mixedAttenuation);
	#endif
#endif
    for (int n = 0; n < min(unity_LightIndicesOffsetAndCount.y, 4); ++n)
    {
        int lightIndex = unity_4LightIndices0[n];
		shadowAttenuation = ShadowAttenuation(lightIndex, s.worldPos);
        mixedAttenuation = MixRealtimeAndBakedShadowAttenuation(shadowAttenuation, bakedShadows, lightIndex, s.worldPos);
		color += GenericLight(lightIndex, s, mixedAttenuation);
    }

	// int lightIndex = unity_4LightIndices0[0];
	// shadowAttenuation = ShadowAttenuation(lightIndex, s.worldPos);
    // mixedAttenuation = MixRealtimeAndBakedShadowAttenuation(shadowAttenuation, bakedShadows, lightIndex, s.worldPos);
	// color += GenericLight(lightIndex, s, mixedAttenuation);

	color += ReflectEnvironment(s, SampleEnvironment(s));
	color += GlobalIllumination(i, s) * s.albedo;
	color += UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Emission);

    return float4(color, albedo.a);
}

#endif // SRP_LIT_INCLUDE