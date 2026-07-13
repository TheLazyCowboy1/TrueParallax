
Shader "TheLazyCowboy1/CustomBlend"
{
Properties 
	{
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	}
	
	Category 
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		ZWrite Off
		Blend Off
		Fog { Color(0,0,0,0) }
		Lighting Off
		Cull Off

		BindChannels 
		{
			Bind "Vertex", vertex
			Bind "texcoord", texcoord 
			//Bind "Color", color 
		}

		SubShader   
		{
				Pass 
			{
				
				
				
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

uniform float LZC_CustomBlend;
sampler2D LZC_BlendWith;

struct v2f {
    float4  pos : SV_POSITION;
    float2  uv : TEXCOORD0;
};

sampler2D _MainTex;
float4 _MainTex_ST;

v2f vert (appdata_full v)
{
    v2f o;
    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
    return o;
}

half4 frag (v2f i) : SV_Target
{
    return lerp(tex2D(_MainTex, i.uv), tex2D(LZC_BlendWith, i.uv), LZC_CustomBlend);
}

ENDCG
				
				
				
			}
		} 
	}
}