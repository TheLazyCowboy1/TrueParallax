using RWCustom;
using UnityEngine;

namespace TrueParallax;

public partial class CameraData
{
    public Vector2 CalcPosCamDiff(Vector2 pos)
    {
        Vector2 centerUV = new(0.5f, 0.5f);
        if (Options.GeneralScale > 1)
        {
            float maxCenterMove = 0.5f * (1 - 1.0f / Options.GeneralScale);
            centerUV = this._camPos - new Vector2(0.5f, 0.5f);
            centerUV = new Vector2(0.5f, 0.5f)
                + new Vector2(Mathf.Clamp(centerUV.x, -maxCenterMove, maxCenterMove), Mathf.Clamp(centerUV.y, -maxCenterMove, maxCenterMove));
        }
        Vector2 uv = (pos - new Vector2(0.5f, 0.5f)) / Options.GeneralScale + centerUV;
        float absBackScale = Mathf.Abs(Options.ConvergenceScale);
        Vector2 posCamDiff = (Vector2.LerpUnclamped(new(0.5f, 0.5f), uv, Options.ConvergenceScale) - this._camPos) * Options.GeneralScale / (absBackScale + 0.5f * (1 - absBackScale));
        if (!Options.DynamicOptimization)
        {
            posCamDiff.x = Mathf.Clamp(posCamDiff.x, -Options.MaxWarp, Options.MaxWarp);
            Vector2 sSize = Custom.rainWorld.screenSize;
            float maxWarpY = Options.MaxWarp * sSize.x / sSize.y; //MaxWarp.y is increased due to aspect ratio
            posCamDiff.y = Mathf.Clamp(posCamDiff.y, -maxWarpY, maxWarpY);
        }
        return posCamDiff;
    }
    public Vector2 CalculateWarp(Vector2 pos, float depth = 5)
    {
        float d = depth >= 30 ? 1 : DepthCurve(depth / 30.0f) / Options.BackgroundDepth;
        return this.currentWarp * (Options.PivotDepth - d) * CalcPosCamDiff(pos); //1 - d, because d=1 => no warp, but d=0 => full warp
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
        Vector2 maxDiff = CalcPosCamDiff(new(this._camPos.x > 0.5f ? 0 : 1, this._camPos.y > 0.5f ? 0 : 1)); //simply use far corner of screen for calculations
        Vector2 sSize = Custom.rainWorld.screenSize;
        maxDiff *= new Vector2(1, sSize.y / sSize.x);
        return currentWarp * Mathf.Max(Mathf.Abs(maxDiff.x), Mathf.Abs(maxDiff.y));
    }
}
