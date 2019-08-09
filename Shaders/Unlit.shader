Shader "SRP/Unlit" {
	Properties {
		_Color("Color", Color) = (1, 1, 1, 1)
	}

	SubShader{
		Pass {
			HLSLPROGRAM
			#pragma multi_compile_instancing
			#pragma instancing_options assumeuniformscaling

			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment

			#include "./Library/Unlit.hlsl"
			ENDHLSL
		}
	}
}
