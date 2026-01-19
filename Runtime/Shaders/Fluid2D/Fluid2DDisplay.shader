Shader "Unlit/Fluid2DDisplay"
{
	Properties {}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _FluidDyeTex;
			sampler2D _FluidVelocityTex;
			sampler2D _FluidPressureTex;

			struct appdata 
			{
				float4 vertex : POSITION; 
				float2 uv : TEXCOORD0; 
			};

			struct v2f 
			{
				float4 pos : SV_POSITION; float2 uv : TEXCOORD0; 
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				fixed3 dye = tex2D(_FluidDyeTex, i.uv);
			    fixed pressure = tex2D(_FluidPressureTex, i.uv).x;
				fixed2 velocity = tex2D(_FluidVelocityTex, i.uv).xy;

				fixed3 normal = normalize(fixed3(velocity, 1));

				// Test values

				//return fixed4(i.uv, 0, 1);
				//return fixed4(length(velocity).xxx, 1);
				return fixed4(velocity, 0, 1);
			}
			ENDHLSL
		}
	}
}
