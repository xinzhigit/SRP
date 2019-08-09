#ifndef SRP_UNLIT_INCLUDE
#define SRP_UNLIT_INCLUDE

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

CBUFFER_START(UnityPerFrame)
	float4x4 unity_MatrixVP;
CBUFFER_END

CBUFFER_START(UnityPerDraw)
	float4x4 unity_ObjectToWorld;
CBUFFER_END

#define UNITY_MATRIX_M unity_ObjectToWorld
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

//CBUFFER_START(UnityPerMaterial)
//	float4 _Color;
//CBUFFER_END
UNITY_INSTANCING_BUFFER_START(PerInstance)
UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
UNITY_INSTANCING_BUFFER_END(PerInstance)

struct VertexInput {
	float4 pos : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput {
	float4 clipPos : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VertexOutput UnlitPassVertex(VertexInput i) {
	VertexOutput o;
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_TRANSFER_INSTANCE_ID(i, o);

	// By making float4(i.pos.xyz, 1.0) explicit we make it possible for the compiler to optimize the computation.
	float4 worldPos = mul(UNITY_MATRIX_M, float4(i.pos.xyz, 1.0));
	o.clipPos = mul(unity_MatrixVP, worldPos);

	return o;
}

float4 UnlitPassFragment(VertexOutput i) : SV_TARGET{
	//return _Color;

	UNITY_SETUP_INSTANCE_ID(i);
	return UNITY_ACCESS_INSTANCED_PROP(PerInstance, _Color);
}

#endif // SRP_UNLIT_INCLUDE