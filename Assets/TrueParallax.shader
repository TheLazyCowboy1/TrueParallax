//Simple parallax shader by LZC

Shader "TheLazyCowboy1/TrueParallax"
{
	Properties 
	{
		[MainTexture] _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
        //LZC_CamPos ("CamPos", Vector) = (0.5, 0.5)
		_LZC_Layer2Tex ("Layer2Tex", 2D) = "black" {}
        LZC_ConvergenceScale ("ConvergenceScale", Float) = 1
        LZC_Warp ("Warp", Float) = 100
        //LZC_MaxWarp ("MaxWarp", Vector) = 1
		LZC_TestNum ("TestNum", Int) = 100
        LZC_StepSize ("StepSize", Float) = 0.01
        //LZC_MoveStepScale ("StepSize", Vector) = 0.01
        LZC_PivotDepth ("PivotDepth", Float) = 1
        LZC_Layer30Depth ("Layer30Depth", Float) = 1
        LZC_AntiAliasingFac ("AntiAliasingFac", Float) = 0
        LZC_BackgroundNoise ("BackgroundNoise", Float) = 0
        LZC_MaxProjection ("MaxProjection", Float) = 0.05
        LZC_CreatureBackgroundTests ("CreatureBackgroundTests", Int) = 10
		LZC_DefaultLevelThickness ("DefaultLevelThickness", Int) = 5
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

			GrabPass { "_ParallaxGrabTex" } //GrabPass to get screen data so that I can then warp it

			Pass 
			{
				
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag

#pragma multi_compile_local _ LZC_PROCESSLAYER2
#pragma multi_compile_local _ LZC_LIMITPROJECTION
#pragma multi_compile_local LZC_LINEARDEPTH LZC_PARABOLICDEPTH LZC_EXTREMEDEPTH LZC_INVERSEDEPTH
#if LZC_PROCESSLAYER2
	#pragma multi_compile_local _ LZC_BUILDCREATUREBACKGROUND
	#pragma multi_compile _ COMBINEDLEVEL
#endif

#include "UnityCG.cginc"

sampler2D _MainTex;
uniform float2 _MainTex_TexelSize;

//sampler2D _LevelTex;
Texture2D<float4> _LevelTex;
uniform float2 _LevelTex_TexelSize;
Texture2D<float4> _PreLevelColorGrab;
Texture2D<float4> _SlopedTerrainMask;

#if LZC_PROCESSLAYER2
//sampler2D _LZC_Layer2Tex;
Texture2D<float4> _LZC_Layer2Tex;
#endif

#if LZC_PROCESSLAYER2
RWTexture2D<float4> _LZC_LevelTex : register(u1);
#else
RWTexture2D<float> _LZC_LevelTex : register(u1);
#endif

uniform float4 _spriteRect;
uniform float2 _screenSize;

uniform float LZC_Layer30Depth;

#if LZC_PROCESSLAYER2 && COMBINEDLEVEL
Texture2D<float4> _OrigLevelTex;
uniform int LZC_DefaultLevelThickness;
#endif


inline half depthCurve(half d) {
#if LZC_PARABOLICDEPTH
	return d*(2 - d); //simple parabola
#elif LZC_EXTREMEDEPTH
	return d*(d*(d - 3) + 3); //much more severe, cubic curve
#elif LZC_INVERSEDEPTH
	return d*d; //squared
#else
	return d; //linear
#endif
}

inline uint depthOfPixel(float r) {
	return (r < 0.997) ? (uint(r * 255.99f) - 1) % 30 : 30;
}
inline uint terrainDep(int2 pos) {
	return round(30 * (2 - 3 * _SlopedTerrainMask.Load(int3(pos, 0)).r));
}

#if LZC_BUILDCREATUREBACKGROUND

//static float2 spriteRectMult = float2(1, 1) / ((_spriteRect.zw - _spriteRect.xy) * _LevelTex_TexelSize * _screenSize);
inline int depthOfTexel(int2 pos) {
	float4 c = _PreLevelColorGrab.Load(int3(pos, 0));
	if (c.r > 1.0f / 255.0f || c.g > 0 || c.b > 0) {
		return 5;
	}

	//int2 textCoord = int2(round((pos - _screenSize * _spriteRect.xy) * spriteRectMult));
	int2 textCoord = pos - int2(_spriteRect.xy * _screenSize);
	return depthOfPixel(_LevelTex.Load(int3(textCoord, 0)).r);
	//float r = _LevelTex.Load(int3(textCoord, 0)).r;
	//return (r < 0.997f) ? ((uint(r*255.99f) - 1) % 30) : 30;
}

uniform int LZC_CreatureBackgroundTests;

#if COMBINEDLEVEL
uniform float LZC_ProjectionMod;
uniform float LZC_MinObjectDepth;
uniform float LZC_MaxDepDiff;
#endif

#define dirCount 2
//#define NONLINEARTESTS
#include "BackgroundBuilder.cginc"

#elif LZC_PROCESSLAYER2
#include "DirectionDefinitions.cginc"
#endif

//returns g and b channels
inline float2 packBits(uint4 backColInts) {
	return float2(
		(backColInts.y | ((backColInts.x & 7) << 5)), //bottom 3 bits +5
		(backColInts.z | ((backColInts.x & 24) << 3)) //top 2 bits +3 //24 = 0b11000
		) / 255.0f;
}

struct v2f {
    float4  pos : SV_POSITION;
    float2  uv : TEXCOORD0;
	float2  suv : TEXCOORD1;
	float2  luv : TEXCOORD2;
};

float4 _MainTex_ST;

v2f vert (appdata_full v)
{
    v2f o;
    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
	o.suv = o.uv * _screenSize;
	//o.luv = (o.uv - _spriteRect.xy) / ((_spriteRect.zw - _spriteRect.xy) * _LevelTex_TexelSize);
	o.luv = (o.uv - _spriteRect.xy) * _screenSize;
    return o;
}

void frag (v2f i)
{
	int2 textCoord = int2(round(i.luv));
	float4 lev = _LevelTex.Load(int3(textCoord, 0));

	int2 checkPos = int2(round(i.suv));

	uint ld = depthOfPixel(lev.r);
	uint d = ld;

	bool terrainMask = false;
	uint td = terrainDep(checkPos);
	if (td < ld) {
		d = td;
		terrainMask = true;
	}

		//check creature mask, if applicable
	bool creatureMask = false;
	if (d > 5) {
		float4 c = _PreLevelColorGrab.Load(int3(checkPos, 0));
		if (c.r > 1.0f / 255.0f || c.g > 0 || c.b > 0) {
			d = 5;
			creatureMask = true;
		}
	}

	//float r = d / 255.0f;
	float r = (d >= 30) ? 1 : depthCurve(d / 30.0f) * LZC_Layer30Depth; //store the depth AFTER the depthCurve instead, as a small optimization


#if LZC_PROCESSLAYER2

		//CREATURES
	if (creatureMask) {
	#if LZC_BUILDCREATUREBACKGROUND
		uint4 backColInts = GenerateBackground(checkPos, LZC_CreatureBackgroundTests, 0, 0, 10, 1);
			//pack in bytes, same as below
		_LZC_LevelTex[checkPos] = float4(r, packBits(backColInts), backColInts.w / 255.0f);
	#else
		_LZC_LevelTex[checkPos] = float4(r,
			1 / 255.0f, //thickness = 1
			0, //layer2 = none
			0); //distance = 0
	#endif
		return;
	}

		//TERRAIN CURVES
	if (terrainMask) {
		_LZC_LevelTex[checkPos] = float4(r,
			31 / 255.0f, //thickness = maximum, so no layer2
			0, //layer2 = not applicable
			0); //distance = 0
		return;
	}
	
		//LEVELTEXCOMBINER
	#if COMBINEDLEVEL
		float4 origLev = _OrigLevelTex.Load(int3(textCoord, 0));
		uint origDep = depthOfPixel(origLev.r);
		if (origDep != ld) { //LevelTexCombiner has altered this pixel's depth
		#if LZC_BUILDCREATUREBACKGROUND
			uint4 backColInts = GenerateBackground(checkPos, LZC_CreatureBackgroundTests, LZC_MinObjectDepth, LZC_ProjectionMod, LZC_MaxDepDiff, LZC_DefaultLevelThickness);
			_LZC_LevelTex[checkPos] = float4(r, packBits(backColInts), backColInts.w / 255.0f);
		#else
			_LZC_LevelTex[checkPos] = float4(r,
				LZC_DefaultLevelThickness / 255.0f, //thickness = DefaultLevelThickness
				0, //layer2 = none
				0); //distance = 0
		#endif

			return; //don't execute anything more
		}
	#endif
	
		//DEFAULT: READ FROM PRE-MADE LAYER2TEX
	float4 backCol = _LZC_Layer2Tex.Load(int3(textCoord, 0));
	uint4 backColInts = uint4(backCol * 255.99f);

		//check if any creatures are interfering with my background
	uint dirIdx = backColInts.w >> 5;
	int lDist = backColInts.w & 31;//0b11111;
	int rDist = backColInts.x;

	fullDirDef //see DirectionDefinitions.cginc

	if (lDist > 0) { //determine if left side is obscured
		int2 lOffset = (dir[dirIdx] * lDist)/2;

		int2 lPos = checkPos + lOffset;
		float4 lCritCol = _PreLevelColorGrab.Load(int3(lPos, 0));
		bool lCrit = lCritCol.r > 1.0f / 255.0f || lCritCol.g > 0 || lCritCol.b > 0;
	#if COMBINEDLEVEL
		if (!lCrit) { //check if LevelTexCombiner is messing this pixel up
			lPos = textCoord + lOffset;
			lCrit = depthOfPixel(_LevelTex.Load(int3(lPos, 0)).r) != depthOfPixel(_OrigLevelTex.Load(int3(lPos, 0)).r); //LevelTex does not match OrigLevelTex
		}
	#endif
		if (lCrit) {
			lDist = 0;
				//set lDist = 0 in packed bits
			backCol.w = (dirIdx << 5) / 255.0f; //because it's packed in w, just re-pack it with lDist = 0
		}
	}
	if (rDist > 0) { //determine if right side is obscured
		int2 lOffset = -(dir[dirIdx] * rDist)/2; //"right" side is negative direction, thus the negative sign

		//copy left-side code for simplicity
		int2 lPos = checkPos + lOffset;
		float4 lCritCol = _PreLevelColorGrab.Load(int3(lPos, 0));
		bool lCrit = lCritCol.r > 1.0f / 255.0f || lCritCol.g > 0 || lCritCol.b > 0;
	#if COMBINEDLEVEL
		if (!lCrit) { //check if LevelTexCombiner is messing this pixel up
			lPos = textCoord + lOffset;
			lCrit = depthOfPixel(_LevelTex.Load(int3(lPos, 0)).r) != depthOfPixel(_OrigLevelTex.Load(int3(lPos, 0)).r); //LevelTex does not match OrigLevelTex
		}
	#endif
		if (lCrit) {
			rDist = 0;
			backColInts.x = 0; //set rDist = 0 in packed bits
		}
	}

		//OBSCURED BY CREATURES
	if (lDist <= 0 && rDist <= 0) { //can't use this background; creatures obscure it
		_LZC_LevelTex[checkPos] = float4(r,
			backCol.y, //normal thickness, but rDist = 0
			0, //no layer2
			0); //no layer2
		return;
	}
	
	_LZC_LevelTex[checkPos] = float4(r, packBits(backColInts), backCol.w);

	//BIT PACKING GUIDE:
	//layer1dep = 5, layer1thick = 5, layer2dep = 5, lDist = 5, rDist = 5, dir = 3;   total = 28
	//r = layer1dep
	//g = layer1thick, rDist(3)
	//b = layer2dep, rDist(2)
	//a = lDist, dir

	//layer2Tex: r = rDist, g = layer1thick, b = layer2dep, a = lDist, dir

#else
	_LZC_LevelTex[checkPos] = r;
#endif

}
ENDCG
				
			}
		
			Pass 
			{
				
CGPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag

#pragma multi_compile_local _ LZC_LIMITPROJECTION
#pragma multi_compile_local _ LZC_DYNAMICOPTIMIZATION
#pragma multi_compile_local _ LZC_PROCESSLAYER2
#pragma multi_compile_local _ LZC_BACKGROUNDNOISE
#if LZC_PROCESSLAYER2
	#pragma multi_compile_local LZC_LINEARDEPTH LZC_PARABOLICDEPTH LZC_EXTREMEDEPTH LZC_INVERSEDEPTH
	#include "DirectionDefinitions.cginc"
#endif

#include "UnityCG.cginc"

sampler2D _MainTex;
uniform float4 _MainTex_TexelSize;

#if defined(SHADER_API_PSSL)
Texture2D<float4> _ParallaxGrabTex;
#else
Texture2D<float4> _ParallaxGrabTex : register(t0);
#endif

sampler2D _NoiseTex;
uniform float4 _NoiseTex_TexelSize;

#if LZC_PROCESSLAYER2
RWTexture2D<float4> _LZC_LevelTex : register(u1);
#else
RWTexture2D<float> _LZC_LevelTex : register(u1);
#endif
//uniform float2 _LZC_LevelTex_TexelSize; //DOES NOT WORK for RWTexture2D
uniform float2 _screenSize;
uniform float4 _spriteRect;

#if LZC_PROCESSLAYER2
Texture2D<float4> _PreLevelColorGrab;
#endif

uniform float2 LZC_CamPos;
uniform float LZC_ConvergenceScale;
uniform float LZC_Warp;
uniform float2 LZC_MaxWarp;
uniform uint LZC_TestNum;
uniform float LZC_StepSize;
uniform float2 LZC_MoveStepScale;
uniform float LZC_PivotDepth;
uniform float LZC_Layer30Depth;
uniform float LZC_AntiAliasingFac;
#if LZC_BACKGROUNDNOISE
uniform float LZC_BackgroundNoise;
#endif
#if LZC_LIMITPROJECTION
uniform float LZC_MaxProjection;
#endif

struct v2f {
    float4  pos : SV_POSITION;
    float2  uv : TEXCOORD0;
	float2  nuv : TEXCOORD1;
	float2  suv : TEXCOORD2;
	float2  posCamDiff : TEXCOORD3;
};

float4 _MainTex_ST;

v2f vert (appdata_full v)
{
    v2f o;
    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
	o.nuv = o.uv * float2(10.667f, 6);
	o.suv = o.uv * _screenSize;
	o.posCamDiff = lerp(float2(0.5f,0.5f), o.uv, LZC_ConvergenceScale) - LZC_CamPos;
    return o;
}

inline half depthCurve(half d) {
#if LZC_PARABOLICDEPTH
	return d*(2 - d); //simple parabola
#elif LZC_EXTREMEDEPTH
	return d*(d*(d - 3) + 3); //much more severe, cubic curve
#elif LZC_INVERSEDEPTH
	return d*d; //squared
#else
	return d; //linear
#endif
}

inline float highFreqNoise(float2 uv, float2 scale) {
	float2 nuv = frac(uv * scale);
	float2 rawLerpFac = 2 * (nuv - float2(0.5f, 0.5f));
	rawLerpFac = rawLerpFac * rawLerpFac; //^2 (also abs)
	float lerpFac = max(rawLerpFac.x, rawLerpFac.y);
	lerpFac = lerpFac * lerpFac; //^4
	lerpFac = lerpFac * lerpFac; //^6
	lerpFac = lerpFac * lerpFac; //^8
	lerpFac = lerpFac * lerpFac; //^10
	float n1 = tex2Dlod(_NoiseTex, float4(nuv, 0, 0)).x;
	float n2 = tex2Dlod(_NoiseTex, float4(nuv + _NoiseTex_TexelSize.xy * 2 * (scale + float2(1,1)), 0, 0)).x; //10 pixel buffer when scale=5
	return lerp(n1, n2, lerpFac * 0.5f);
}


half4 frag (v2f i) : SV_Target
{

	//OPTIMIZATION

#if LZC_DYNAMICOPTIMIZATION
	float2 moveStep = i.posCamDiff;
#else
	float2 moveStep = clamp(i.posCamDiff, -LZC_MaxWarp, LZC_MaxWarp);
#endif

		//scale moveStep up to its proper size
	moveStep = moveStep * LZC_MoveStepScale; //use a global var to scale it instead
	float2 unoptimizedMoveStep = moveStep; //not changed by DYNAMICOPTIMIZATION

	uint totalTests = LZC_TestNum;
	float stepSize = LZC_StepSize;

		//Do this before scaling values! If it's done afterward, then there will be noticeable lines where totalTests change
	float noiseOffset = 0;
	if (LZC_AntiAliasingFac > 0.001f) { //allegedly, this if statement should be fast because AntiAliasingFac is a uniform var
		float noiseVal = tex2D(_NoiseTex, i.nuv).x;
		noiseOffset = LZC_AntiAliasingFac * (noiseVal - 0.2f);// * saturate((noiseVal - 0.3f) * 2);
	}

	float2 initGrabPos = i.suv - moveStep * (totalTests + noiseOffset) * LZC_PivotDepth; //start at the END and then move BACKWARDS

#if LZC_DYNAMICOPTIMIZATION
	float2 absCamDiff = abs(i.posCamDiff);
	if (absCamDiff.x < LZC_MaxWarp.x && absCamDiff.y < LZC_MaxWarp.y) { //don't optimize above MaxWarp, because those wouldn't actually be optimizations
		float2 adjustedDiff = LZC_MaxWarp / absCamDiff;
		float optimization = min(min(adjustedDiff.x, adjustedDiff.y), 0.25f * LZC_TestNum); //can't be less than 4 totalTests
		stepSize = stepSize * optimization;
		moveStep = moveStep * optimization;
		totalTests = ceil(totalTests / optimization);
	}
	#if LZC_PROCESSLAYER2
	float minThickness = stepSize; //otherwise, layer1 can seemingly just disappear when stepSize is high
	#endif
#endif

#if LZC_LIMITPROJECTION
	float maxXDist = max(LZC_MaxProjection, stepSize);
#endif
#if LZC_BACKGROUNDNOISE
	float bestXDist = 1;
	float bestDep = 1;
#endif
#if LZC_PROCESSLAYER2
	uint bestLayer = 1;
#endif

	int2 bestGrabPos = int2(round(i.suv));

	float percentage = 0.0002f; //very tiny margin of error, just in case there's some weird imprecision error
	float2 grabPos = initGrabPos + float2(0.5f, 0.5f); //adjust coords slightly so that int2(round(grabPos)) becomes int2(grabPos)

		//LOOP

	uint c = 0;
	[loop]
	while(c <= totalTests) {

			//CALCULATE XDISTANCE

		int2 checkPos = int2(grabPos);
#if LZC_PROCESSLAYER2
		float4 lev = _LZC_LevelTex[checkPos];
		float newDepth = lev.x;
#else
		float newDepth = _LZC_LevelTex[checkPos];
#endif
		float xDistance = percentage - newDepth;

			//CHECK XDISTANCE

#if LZC_PROCESSLAYER2 && LZC_LIMITPROJECTION
			//THIS IS GETTING CRAZY

		if (xDistance >= 0) {
			float thickness = (uint(lev.y * 255.99f) & 31) / 30.0f;
	#if LZC_DYNAMICOPTIMIZATION
			thickness = max(thickness, minThickness);
	#endif
			if (xDistance < thickness) {
				bestGrabPos = checkPos;
				bestLayer = 1;
	#if LZC_BACKGROUNDNOISE
				bestXDist = xDistance;
				bestDep = newDepth;
	#endif
				break;
			}
			uint l2DepInt = uint(lev.z * 255.99f) & 31;
			if (l2DepInt > 0) { //0 means that there is no layer2 for this texel
				newDepth = l2DepInt >= 30
					? 1
					: depthCurve(l2DepInt / 30.0f) * LZC_Layer30Depth;
				xDistance = percentage - newDepth;
				if (xDistance >= 0) {
					if (xDistance < maxXDist) {
						bestGrabPos = checkPos;
						bestLayer = 2;
	#if LZC_BACKGROUNDNOISE
						bestXDist = xDistance;
						bestDep = newDepth;
	#endif
						break;
					}
				}
				else {
					bestGrabPos = checkPos;
					bestLayer = 2;
	#if LZC_BACKGROUNDNOISE
					bestXDist = min(bestXDist, -xDistance);
					bestDep = newDepth;
	#endif
				}
			}
		}
		else {
			bestGrabPos = checkPos;
			bestLayer = 1;
	#if LZC_BACKGROUNDNOISE
			bestXDist = min(bestXDist, -xDistance);
			bestDep = newDepth;
	#endif
		}

#elif LZC_LIMITPROJECTION
			//HAS A LITTLE EXTRA LOGIC

		if (xDistance >= 0) {
			if (xDistance < maxXDist) {
				bestGrabPos = checkPos;
	#if LZC_BACKGROUNDNOISE
				bestXDist = xDistance;
				bestDep = newDepth;
	#endif
				break; //we found it! don't run any more code, ideally
			}
		}
		else {
			bestGrabPos = checkPos;
	#if LZC_BACKGROUNDNOISE
			bestXDist = min(bestXDist, -xDistance);
			bestDep = newDepth;
	#endif
		}

#elif LZC_PROCESSLAYER2
			//BASICALLY LIMITPROJECTION EXCEPT IT USES THICKNESS INSTEAD OF MAXXDIST

		if (xDistance >= 0) {
			float thickness = (uint(lev.y * 255.99f) & 31) / 30.0f;
	#if LZC_DYNAMICOPTIMIZATION
			thickness = max(thickness, minThickness);
	#endif
			if (xDistance < thickness) {
				bestGrabPos = checkPos;
				//bestLayer = 1; //in this implementation, bestLayer should always be 1 here anyway
	#if LZC_BACKGROUNDNOISE
				bestXDist = xDistance;
				bestDep = newDepth;
	#endif
				break;
			}
			newDepth = (uint(lev.z * 255.99f) & 31) / 30.0f;
			newDepth = newDepth >= 1
				? 1
				: depthCurve(newDepth) * LZC_Layer30Depth;
			xDistance = percentage - newDepth;
			if (newDepth > 0 && xDistance >= 0) {
				bestGrabPos = checkPos;
				bestLayer = 2;
	#if LZC_BACKGROUNDNOISE
				bestXDist = xDistance;
				bestDep = newDepth;
	#endif
				break;
			}
		}
		else {
			bestGrabPos = checkPos;
			//bestLayer = 1; //in this implementation, bestLayer should always be 1 here anyway
	#if LZC_BACKGROUNDNOISE
			bestXDist = min(bestXDist, -xDistance);
			bestDep = newDepth;
	#endif
		}

#else
		//OBVIOUSLY WAY SIMPLER

		if (xDistance >= 0) {
			bestGrabPos = checkPos;
	#if LZC_BACKGROUNDNOISE
			bestXDist = xDistance;
			bestDep = newDepth;
	#endif
			break;
		}
#endif

			//manually increment counters
		grabPos = grabPos + moveStep;
		percentage = percentage + stepSize;
		c = c + 1;
	}


//APPLY FINAL NOISE

#if LZC_PROCESSLAYER2
	half4 finalCol;
	if (bestLayer > 1) {
		uint4 lev = int4(_LZC_LevelTex.Load(int3(bestGrabPos, 0)) * 255.99f);
		uint d = lev.w >> 5;
		int lDist = lev.w & 31;//0b11111;
		int rDist = (lev.y >> 5) | ((lev.z & 96) >> 2); //96 = 0b01100000

		fullDirDef //see DirectionDefinitions.cginc

		int2 lPos = bestGrabPos + (dir[d] * lDist)/2;
		int2 rPos = bestGrabPos - (dir[d] * rDist)/2;
		if (lDist > 0 && rDist > 0) { //both are usable, so lerp between the two colors
			float4 lCol = _ParallaxGrabTex.Load(int3(lPos, 0));
			float4 rCol = _ParallaxGrabTex.Load(int3(rPos, 0));
			float totalDist = lDist + rDist;
			finalCol = (lCol * rDist + rCol * lDist) / totalDist;//lerp(lCol, rCol, lDist / float(lDist + rDist));
		}
		else {
			int dist3 = 0; //for the noise stuff below
			if (lDist > 0) { //left is good
				finalCol = _ParallaxGrabTex.Load(int3(lPos, 0)); //so just use left side
				dist3 = lDist;
			}
			else { //right is good
				finalCol = _ParallaxGrabTex.Load(int3(rPos, 0)); //so just use right side
				dist3 = rDist;
			}
		#if LZC_BACKGROUNDNOISE
			if (bestXDist <= stepSize) { //change how the noise is applied
				bestXDist = dist3 / LZC_Warp; //noise severity = how far away from pixel
				c = 0; //act like it's LIMITPROJECTION
			}
		#endif
		}
	}
	else {
		finalCol = _ParallaxGrabTex.Load(int3(bestGrabPos, 0));
	}

#else
	half4 finalCol = _ParallaxGrabTex.Load(int3(bestGrabPos, 0));
#endif

#if !LZC_BACKGROUNDNOISE
	return finalCol;
#else

	if (bestXDist <= stepSize || bestDep >= 1) { //it's close enough; don't add noise. Also don't add noise to the sky
		return finalCol;
	}

	float2 noisePoint;
#if LZC_LIMITPROJECTION || LZC_PROCESSLAYER2
	if (c > totalTests) { //loop did NOT break
		noisePoint = (initGrabPos //start at starting pos
			+ unoptimizedMoveStep * LZC_TestNum * bestDep) //go "bestDep" of the way towards the ending pos
			/ _screenSize; //convert from texel coordinates to uv
	}
	else
#endif
		//logic if the loop DID break. This is always used if we're not using LIMITPROJECTION
	noisePoint = (bestGrabPos //start at grabPos
		+ float2(bestXDist, bestXDist) * LZC_Warp*0.5f) //fixed offset based on bestXDist and Warp; *0.5f because I think it'll look better
		/ _screenSize; //convert from texel coordinates to uv

	float noiseVal2 = highFreqNoise(noisePoint - _spriteRect.xy, float2(5.333f, 3)); //subtract spriteRect.xy so that noise doesn't appear to move when the screen is moving

	float curBrightness = finalCol.r * 0.299f + finalCol.g * 0.587f + finalCol.b * 0.114f;
	half add = bestXDist * LZC_BackgroundNoise * (noiseVal2 - 0.5f) * (curBrightness + 0.3f); //more noise if pixel is already brighter
	finalCol.x += add;
	finalCol.y += add;
	finalCol.z += add;
	return finalCol;
#endif

}
ENDCG
				
			}
		} 
	}
}