#ifndef SRP_SHADOWCASTER_INCLUDE
#define SRP_SHADOWCASTER_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

UNITY_INSTANCING_BUFFER_START(PerInstance)
	UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

CBUFFER_START(_ShadowCasterBuffer)
	float _ShadowBias;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	float _Cutoff;
CBUFFER_END

struct VertexInput {
	float4 pos : POSITION;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	float2 uv : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput ShadowCasterPassVertex(VertexInput i) {
	VertexOutput o;
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);

    float4 worldPos = mul(UNITY_MATRIX_M, float4(i.pos.xyz, 1.0));
    o.clipPos = mul(unity_MatrixVP, worldPos);

	o.uv = TRANSFORM_TEX(i.uv, _MainTex);

	// This is sufficient to render shadows, but it is possible for shadow casters to intersect the near place, 
	// which can cause holes to appear in shadows. 
	// To prevent this, we have to clamp the vertices to the near place in the vertex program. 
	// This is done by taking the maximum of the Z coordinate and the W coordinate of the clip-space position.
#if UNITY_REVERSED_Z
	o.clipPos.z -= _ShadowBias;
	o.clipPos.z = min(o.clipPos.z, o.clipPos.w * UNITY_NEAR_CLIP_VALUE);
#else
    o.clipPos.z += _ShadowBias;
    o.clipPos.z = max(o.clipPos.z, o.clipPos.w * UNITY_NEAR_CLIP_VALUE);
#endif

    return o;
}

float4 ShadowCasterPassFragment(VertexOutput i) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(i);

#if !defined(_CLIPPING_OFF)
	float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
	alpha *= UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color).a;
	clip(alpha - _Cutoff);
#endif

    return 0;
}

#endif // SRP_SHADOWCASTER_INCLUDE