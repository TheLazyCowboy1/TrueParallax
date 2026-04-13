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

#if LZC_SIMPLERLAYERS
	#define dirCount 4
#else
	#define dirCount 8
#endif

#include "UnityCG.cginc"

//sampler2D _MainTex;
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

inline int depthOfTexel(int2 pos) {
	float r = _MainTex.Load(int3(pos, 0)).r;
	return (r < 0.997f) ? ((uint)round(r*255 - 1) % 30) : 30;
}

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
	int origDep = depthOfTexel(startPos);

	if (origDep >= 30) {
		//discard;
		return float4(0, 31 / 255.0f, 0, 0); //fully thick layer1; otherwise irrelevant data so just 0
	}

	int2 dir[dirCount];
#if LZC_SIMPLERLAYERS
	dir[0] = int2(1, 0);
	dir[1] = int2(0, 1);
	dir[2] = int2(1, 1);
	dir[3] = int2(1, -1); //(equivalent to -1,1)
#else
	dir[0] = int2(2, 0);
	dir[1] = int2(0, 2);
	dir[2] = int2(2, 2);
	dir[3] = int2(2, -2); //(equivalent to -1,1)
	dir[4] = int2(2, 1); //1, 0.5
	dir[5] = int2(1, 2); //0.5, 1
	dir[6] = int2(-1, 2); //-0.5, 1
	dir[7] = int2(2, -1); //1, -0.5
#endif

	int lDist[dirCount], rDist[dirCount], lDep[dirCount], rDep[dirCount];
	[unroll(dirCount)]
	for (uint b = 0; b < dirCount; b++) { lDist[b] = 0; rDist[b] = 0; lDep[b] = 0; rDep[b] = 0; }
	uint bestDir = 8;

	[loop]
	for (int c = 1; c <= LZC_BackgroundTestNum; c++) {
		int halfC = (int)(LZC_MinObjectDepth + c * 0.5f * LZC_ProjectionMod);
		int targetDep = origDep + halfC;
		if (targetDep >= 29) { //no longer possible to find any viable pixels, so just break out of the loop
			break;
		}

		[unroll(dirCount)]
		for (uint d = 0; d < dirCount; d++) {
#if LZC_SIMPLERLAYERS
			int2 offset = dir[d] * c;
#else
			int2 offset = (dir[d] * c) / 2;
#endif
			if (lDist[d] == 0) {
				int dep = depthOfTexel(startPos + offset);
				if (dep > targetDep) {
					lDist[d] = c;
					lDep[d] = dep;
					if (rDist[d] > 0) {
						bestDir = d;
						break;
					}
				}
			}
			if (rDist[d] == 0) {
				int dep = depthOfTexel(startPos - offset);
				if (dep > targetDep) {
					rDist[d] = c;
					rDep[d] = dep;
					if (lDist[d] > 0) {
						bestDir = d;
						break;
					}
				}
			}
		}

		if (bestDir < 8) { //we've already made our pick
			break;
		}
	}

	if (bestDir < 8) { //a ray had both sides hit

		int dist2 = rDist[bestDir];
		float layer1thick = ceil(LZC_MinObjectDepth + (lDist[bestDir] + rDist[bestDir]) * 0.5f * LZC_ProjectionMod);
		int l2Dep = 0;
		int dist1 = lDist[bestDir]; //must be int so bit operation works

		if (abs(lDep[bestDir] - rDep[bestDir]) <= layer1thick * LZC_MaxDepDiff) {
			float totalDist = lDist[bestDir] + rDist[bestDir];
			l2Dep = round((lDep[bestDir] * rDist[bestDir] + rDep[bestDir] * lDist[bestDir]) / totalDist); //basically a weighted average, where the weight of lDep = rDist
		}
		else { //the two pixels are too different to interpolate between, so just pick the closest one
			if (rDist[bestDir] < lDist[bestDir]) { //right is closer
				dist1 = 0;
				l2Dep = rDep[bestDir];
			}
			else { //left is closer
				dist2 = 0;
				l2Dep = lDep[bestDir];
			}
		}

		return float4(
			dist2,
			clamp(layer1thick, 1, 31),
			l2Dep,
			dist1 | (bestDir << 5)
		) / 255.0f;
	}

		//no rays hit both sides; let's see if any rays had at least 1 side hit, though

		//find the shortest distance first
	int minDist = LZC_BackgroundTestNum+1;
	[unroll(dirCount)]
	for (uint k = 0; k < dirCount; k++) {
		if (lDist[k] > 0 && lDist[k] < minDist) {
			minDist = lDist[k];
		}
		if (rDist[k] > 0 && rDist[k] < minDist) {
			minDist = rDist[k];
		}
	}

	if (minDist > LZC_BackgroundTestNum) { //absolutely no background for this
		return float4(0, 31 / 255.0f, 0, 0);
	}

		//find the greatest depth that matches the shortest distance
	int maxDep = 0;
	[unroll(dirCount)]
	for (uint l = 0; l < dirCount; l++) {
		if (lDist[l] == minDist && lDep[l] > maxDep) {
			maxDep = lDep[l];
			bestDir = l;
		}
		if (rDist[l] == minDist && rDep[l] > maxDep) {
			maxDep = rDep[l];
			bestDir = l;
		}
	}

		//pack info into bytes
	float layer1thick = ceil(LZC_MinObjectDepth + (minDist + LZC_BackgroundTestNum+1) * 0.5f * LZC_ProjectionMod);
	return float4(
		rDist[bestDir],
		clamp(layer1thick, 1, 31),
		maxDep,
		lDist[bestDir] | (bestDir << 5)
	) / 255.0f;

}
ENDCG
				
			}
		} 
	}
}