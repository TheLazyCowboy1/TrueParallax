//r = dist2,
//g = layer1thick,
//b = layer2dep,
//a = dist1, dir

Shader "TheLazyCowboy1/ThicknessMap"
{
	Properties 
	{
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		LZC_BackgroundTestNum ("BackgroundTestNum", Int) = 22
		LZC_ProjectionMod ("ProjectionMod", Float) = 0.5
		LZC_MinObjectDepth ("MinObjectDepth", Float) = 1
		LZC_MaxDepDiff ("MaxDepDiff", Float) = 1
	}
	
	Category 
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
		ZWrite Off
		Blend Off
		Fog { Color(0,0,0,0) }
		Lighting Off
		Cull Off //we can turn backface culling off because we know nothing will be facing backwards

		BindChannels 
		{
			Bind "Vertex", vertex
			Bind "texcoord", texcoord
		}

		SubShader   
		{	
		
			Pass 
			{
				
				
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag

			#pragma multi_compile_local _ LZC_SIMPLERLAYERS

#include "UnityCG.cginc"

Texture2D<float4> _MainTex;
uniform float2 _MainTex_TexelSize;

uniform int LZC_BackgroundTestNum;
uniform float LZC_ProjectionMod;
uniform float LZC_MinObjectDepth;
uniform float LZC_MaxDepDiff;

struct v2f {
    float4  pos : SV_POSITION;
    float2  uv : TEXCOORD0;
	float2  texel : TEXCOORD1;
};

float4 _MainTex_ST;

v2f vert (appdata_full v)
{
    v2f o;
    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
	o.texel = o.uv / _MainTex_TexelSize;
    return o;
}


#if LZC_SIMPLERLAYERS
	#define dirCount 4
#else
	#define dirCount 8
#endif
inline int depthOfTexel(int2 pos) {
	float r = _MainTex.Load(int3(pos, 0)).r;
	return (r < 0.997f) ? ((uint(r*255.99f) - 1) % 30) : 30;
}
#include "BackgroundBuilder.cginc"


float4 frag (v2f i) : SV_Target
{
	/* OUTDATED DESCRIPTION (still better than nothing)
		send out 8 "rays" of 11px each
		each ray searches for the first pixel deeper than the current one
		records the estimated "depth" of the current pixel by taking the max of each opposite pair of rays' sum of distance before finding a deeper pixel
	*/
	/*
		left, right
		up, down
		dul, ddr (diagonal-upleft, diagonal-downright)
		ddl, dur (diagonal-downleft, diagonal-upright)
	*/

	int2 startPos = int2(i.texel);//int2(round(i.texel));
	return GenerateBackground(startPos, LZC_BackgroundTestNum, LZC_MinObjectDepth, LZC_ProjectionMod, LZC_MaxDepDiff, 31);

}
ENDCG
				
			}
		} 
	}
}