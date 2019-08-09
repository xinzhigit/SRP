#ifndef SRP_LIGHTING_INCLUDE
#define SRP_LIGHTING_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"

TEXTURECUBE(unity_SpecCube0);
TEXTURECUBE(unity_SpecCube1);
SAMPLER(sampleruity_SpecCube0);

struct SurfaceData {
	float3 normal;
	float3 worldPos;
	float3 viewDir;
	float3 albedo;
	float3 specular;
	float roughness;
	float perceptualRoughness;
	float fresnelStrength;
	float reflectivity;
	bool vertexLight;
};

SurfaceData GetSurfaceData(float3 normal, float3 worldPos, float3 viewDir, float3 albedo, float metallic, float smoothness, bool vertexLight = false) {
	SurfaceData s;
	s.normal = normal;
	s.worldPos = worldPos;
	s.viewDir = viewDir;
	s.albedo = albedo;
	s.vertexLight = vertexLight;
	if (vertexLight) {
		s.reflectivity = 0.0;
		s.specular = 0.0;
	}
	else {
		s.specular = lerp(0.04, albedo, metallic);
		s.reflectivity = lerp(0.04, 1.0, metallic);
		s.albedo *= 1.0 - s.reflectivity;
	}
	s.perceptualRoughness = 1 - smoothness;
	s.roughness = s.perceptualRoughness * s.perceptualRoughness;
	s.fresnelStrength = saturate(smoothness + s.reflectivity);

	return s;
}

SurfaceData GetSurfaceDataVertex(float3 normal, float3 worldPos) {
	return GetSurfaceData(normal, worldPos, 0, 1, 0, 0, true);
}

float3 LightSurface(SurfaceData s, float3 lightDir) {
	float3 color = s.albedo;
	if (!s.vertexLight) {
		float3 halfDir = SafeNormalize(lightDir + s.viewDir);
		float nh = saturate(dot(s.normal, halfDir));
		float lh = saturate(dot(lightDir, halfDir));
		float d = nh * nh * (s.roughness * s.roughness - 1.0) + 1.00001;
		float normalizationTerm = s.roughness * 4.0 + 2.0;
		float specularTerm = s.roughness * s.roughness;
		specularTerm /= (d * d) * max(0.1, lh * lh) * normalizationTerm;
		color += specularTerm * s.specular;
	}

	return color * saturate(dot(s.normal, lightDir));
}

float3 ReflectEnvironment(SurfaceData s, float3 environment) {
	if (s.vertexLight) {
		return 0;
	}

	float fresnel = Pow4(1.0 - saturate(dot(s.normal, s.viewDir)));
	environment *= lerp(s.specular, s.fresnelStrength, fresnel);
	environment /= s.roughness * s.roughness + 1.0;

	return environment;
}

void PremultiplyAlpha(inout SurfaceData s, inout float alpha) {
	s.albedo *= alpha;
	alpha = lerp(alpha, 1, s.reflectivity);
}

SurfaceData GetLitSurfaceMeta(float3 color, float metallic, float smoothness) {
	return GetSurfaceData(0, 0, 0, color, metallic, smoothness);
}

#endif // SRP_LIGHTING_INCLUDE