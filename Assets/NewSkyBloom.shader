// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

    
// Upgrade NOTE: replaced 'samplerRECT' with 'sampler2D'

//from http://forum.unity3d.com/threads/68402-Making-a-2D-game-for-iPhone-iPad-and-need-better-performance


Shader "TheLazyCowboy1/NewSkyBloom" //Unlit Transparent Vertex Colored Additive 
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
        GrabPass { }
        
                Pass 
            {
                
                
                
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
#include "_ShaderFix.cginc"
#include "_RippleClip.cginc"

//#pragma profileoption NumTemps=64
//#pragma profileoption NumInstructionSlots=2048

sampler2D _MainTex;
sampler2D _NoiseTex;
sampler2D _PalTex;

#if defined(SHADER_API_PSSL)
sampler2D _GrabTexture;
#else
sampler2D _GrabTexture : register(s0);
#endif

uniform float _RAIN;

uniform float4 _spriteRect;
uniform float2 _screenSize;


struct v2f {
    float4  pos : SV_POSITION;
   float2  uv : TEXCOORD0;
    float2 scrPos : TEXCOORD1;
    float4 clr : COLOR;
};

float4 _MainTex_ST;

v2f vert (appdata_full v)
{
    v2f o;
    o.pos = UnityObjectToClipPos (v.vertex);
    o.uv = TRANSFORM_TEX (v.texcoord, _MainTex);
    o.scrPos = ComputeScreenPos(o.pos);
    o.clr = v.color;
    return o;
}

half4 frag (v2f i) : SV_Target
{

half2 screenPos = half2(i.scrPos.x, i.scrPos.y);

     half4 texcol = half4(0,0,0,1);
    float div = 1.0;
    float coef = 1.0;
    float fI = 0;
    
    //half  amount = pow(clamp(1- distance(half2(0.2,0.2), screenPos.xy), 0, 1), 0.5);
    //amount *= i.clr.w; //amount isn't even used??
    //float _SkyBlurAmount = 0.0018;
    float horFac = _screenSize.y / _screenSize.x;
    float2 _SkyBlurAmount = float2(0.0018 * horFac, 0.0018);
    
    half4 gCol = half4(0,0,0,0);
    half4 skyColor = tex2D(_PalTex, float2(0, 7));
    
#if defined(SHADER_API_SWITCH)
    const int loopInc = 2;

    for (int j = 0; j < 6; j += loopInc) {
        fI++;
        coef = pow(0.62, j + 1);
#else
    for (int j = 0; j < 6; j++) {
        fI++;
        coef*=0.62;
#endif
        
        gCol = tex2D(_GrabTexture, screenPos + _SkyBlurAmount * float2(-fI, fI));//float2(screenPos.x - fI * _SkyBlurAmount * horFac, screenPos.y + fI * _SkyBlurAmount));
        if (gCol.x == skyColor.x && gCol.y == skyColor.y && gCol.z == skyColor.z) {
            texcol += gCol * coef;
            div += coef;
        }
        
        gCol = tex2D(_GrabTexture, screenPos + _SkyBlurAmount * float2(fI, fI));//float2(screenPos.x + fI * _SkyBlurAmount * horFac, screenPos.y + fI * _SkyBlurAmount));
        if (gCol.x == skyColor.x && gCol.y == skyColor.y && gCol.z == skyColor.z) {
            texcol += gCol * coef;
            div += coef;
        }
        
        gCol = tex2D(_GrabTexture, screenPos + _SkyBlurAmount * float2(fI, -fI));//float2(screenPos.x + fI * _SkyBlurAmount * horFac, screenPos.y - fI * _SkyBlurAmount));
        if (gCol.x == skyColor.x && gCol.y == skyColor.y && gCol.z == skyColor.z) {
            texcol += gCol * coef;
            div += coef;
        }
        
        gCol = tex2D(_GrabTexture, screenPos + _SkyBlurAmount * float2(-fI, -fI));//float2(screenPos.x - fI * _SkyBlurAmount * horFac, screenPos.y - fI * _SkyBlurAmount));
        if (gCol.x == skyColor.x && gCol.y == skyColor.y && gCol.z == skyColor.z){
            texcol += gCol * coef;
            div += coef;
        }
    }
 
 half4 grabCol= tex2D(_GrabTexture, float2(screenPos.x, screenPos.y));
 grabCol += texcol*0.5;
 div *= 0.75;
 texcol = (grabCol + texcol) / div;

   grabCol.x = max(grabCol.x, texcol.x);
   grabCol.y = max(grabCol.y, texcol.y);
   grabCol.z = max(grabCol.z, texcol.z);
 
     smoothRippleClip(grabCol,i.scrPos);
 
    return grabCol;
 
}
ENDCG
                
                
                
            }
        } 
    }
}
