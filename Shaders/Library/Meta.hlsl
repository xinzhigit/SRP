#ifndef SRP_LIT_META_INCLUDE
#define SRP_LIT_META_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Lighting.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;
CBUFFER_END

CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	float4 _Color;
	float4 _Emission;
	float _Metallic;
	float _Smoothness;
CBUFFER_END

CBUFFER_START(UnityMetaPass)
	float unity_OneOverOutputBoost;
	float unity_MaxOutputValue;
	bool4 unity_MetaVertexControl;
	bool4 unity_MetaFragmentControl;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

struct VertexInput {
	float4 pos : POSITION;
	float2 uv : TEXCOORD0;
	float2 lightmapUV : TEXCOORD1;
	float2 dynamicLightmapUV : TEXCOORD2;
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	float2 uv : TEXCOORD0;
};

VertexOutput MetaPassVertex(VertexInput i) {
	VertexOutput o;

	if (unity_MetaVertexControl.x) {
		i.pos.xy = i.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	}
	if (unity_MetaVertexControl.y) {
		i.pos.xy = i.dynamicLightmapUV * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
	}
	
	i.pos.z = i.pos.z > 0 ? FLT_MIN : 0.0f;

	// By making float4(i.pos.xyz, 1.0) explicit we make it possible for the compiler to optimize the computation.
	o.clipPos = mul(unity_MatrixVP, float4(i.pos.xyz, 1.0));
	o.uv = TRANSFORM_TEX(i.uv, _MainTex);

	return o;
}

float4 MetaPassFragment(VertexOutput i) : SV_TARGET {
	float4 meta = 0;
	float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
	albedo *= _Color;
	albedo.rgb *= albedo.a;

	if (unity_MetaFragmentControl.x) {
		SurfaceData s = GetLitSurfaceMeta(albedo, _Metallic, _Smoothness);

		meta = float4(s.albedo, 1);
		//meta.rgb += s.specular * s.roughness * 0.5;
		meta.rgb = clamp(PositivePow(meta.rgb, unity_OneOverOutputBoost), 0, unity_MaxOutputValue);
	}
	if (unity_MetaFragmentControl.y) {
		meta = float4(_Emission.rgb * albedo.a, 1);
	}

	return meta;
}

#endif // SRP_LIT_META_INCLUDE