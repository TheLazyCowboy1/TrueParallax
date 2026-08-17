//Copy of Futile/Basic shader, with slight change
Shader "TheLazyCowboy1/ForegroundCreature" //Unlit Transparent Vertex Colored
{
	Properties 
	{
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	}
	
	Category 
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		ZWrite Off
		//Alphatest Greater 0
		Blend SrcAlpha OneMinusSrcAlpha 
		Fog { Color(0,0,0,0) }
		Lighting Off
		Cull Off //we can turn backface culling off because we know nothing will be facing backwards

		BindChannels 
		{
			Bind "Vertex", vertex
			Bind "texcoord", texcoord 
			Bind "Color", color 
		}

		SubShader   
		{
			/*
			Pass 
			{
				SetTexture [_MainTex] 
				{
					Combine texture * primary
				}
			}
			*/
			
				Pass 
			{
				
				
				
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag

#include "UnityCG.cginc"

struct v2f {
    float4  pos : SV_POSITION;
    float2  uv : TEXCOORD0;
	float2  suv : TEXCOORD1;
    float4 clr : COLOR;
};

sampler2D _MainTex;
float4 _MainTex_ST;

RWTexture2D<float4> _LZC_LevelTex : register(u1);
uniform float2 _screenSize;

v2f vert (appdata_full v)
{
    v2f o;
    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
	o.suv = ComputeScreenPos(o.pos) * _screenSize;
    o.clr = v.color;
    return o;
}

half4 frag (v2f i) : SV_Target
{
	float4 result = tex2D(_MainTex, i.uv) * i.clr;
	float3 rgb = result.rgb * result.a;
	if (rgb.r > 0 || rgb.g > 0 || rgb.b > 0) {
		_LZC_LevelTex[int2(i.suv)] = float4(1,1,1,1);
	}
    return result;
}

ENDCG
				
				
				
			}
		} 
	}
}