using RWCustom;
using Unity.Mathematics;
using UnityEngine;

namespace TrueParallax;

public partial class CameraData
{
    private static Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max)
        => new(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y));
    public Vector2 CalcPosCamDiff(Vector2 pos)
    {
        //Apply LZC_GeneralScale
        float generalScale = 1.0f / Options.GeneralScale;
        Vector2 half = new(0.5f, 0.5f);
        Vector2 maxCenterMove = half * (1 - Mathf.Clamp01(generalScale)); //saturate because it goes crazy outside that range
        Vector2 centerUV = half + Clamp(this.drawCamPos - half, -maxCenterMove, maxCenterMove);
        Vector2 uv = (pos - half) * Options.GeneralScale + centerUV;

        //Apply LZC_ConvergenceScale
        float absBackScale = Mathf.Abs(Options.ConvergenceScale); //prevents ridiculous results when BackgroundScale is < 0, especially: -1 caused division by 0
        Vector2 posCamDiff = (Vector2.LerpUnclamped(centerUV, uv, Options.ConvergenceScale) - this.drawCamPos) / (generalScale * (absBackScale + 0.5f * (1 - absBackScale)));

        Vector2 sSize = Custom.rainWorld.screenSize;
        if (!Options.DynamicOptimization)
        {
            posCamDiff.x = Mathf.Clamp(posCamDiff.x, -Options.MaxWarp, Options.MaxWarp);
            float maxWarpY = Options.MaxWarp * sSize.x / sSize.y; //MaxWarp.y is increased due to aspect ratio
            posCamDiff.y = Mathf.Clamp(posCamDiff.y, -maxWarpY, maxWarpY);
        }
        return posCamDiff * new Vector2(1, sSize.y / sSize.x); //scale y component
    }
    public float EffectiveDepthMod(float depth = 5)
    {
        return Options.PivotDepth - (depth >= 30 ? 1 : DepthCurve(depth / 30.0f) / Options.BackgroundDepth);
    }
    public Vector2 CalculateWarp(Vector2 pos, float depth = 5)
    {
        return this.currentWarp * EffectiveDepthMod(depth) * CalcPosCamDiff(pos); //1 - d, because d=1 => no warp, but d=0 => full warp
    }

    public float DepthCurve(float d) => Options.DepthCurve switch
        {
            Options.DepthCurveOptions.CUBIC => d * (d * (d - 3) + 3),
            Options.DepthCurveOptions.PARABOLIC => d * (1.8f - 0.8f*d),
            Options.DepthCurveOptions.INVERSE => d * d,
            Options.DepthCurveOptions.REALAPPROX => Mathf.Lerp(d, d * (d * (d - 3) + 3), 0.001f*Mathf.Abs(currentWarp)),
            Options.DepthCurveOptions.REALISTIC => 1 - 1.0f / (6*d * Mathf.Abs(currentWarp)/Custom.rainWorld.screenSize.x + 1),
            _ => d //LINEAR
        };

    public float CalcMaxUsedWarp()
    {
        Vector2 maxDiff = CalcPosCamDiff(new(this.drawCamPos.x > 0.5f ? 0 : 1, this.drawCamPos.y > 0.5f ? 0 : 1)); //simply use far corner of screen for calculations
        return currentWarp * Mathf.Max(Mathf.Abs(maxDiff.x), Mathf.Abs(maxDiff.y));
    }
}
