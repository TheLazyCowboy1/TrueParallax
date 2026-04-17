using RWCustom;
using UnityEngine;

namespace TrueParallax;

public partial class CameraData
{
    public Vector2 CalculateWarp(Vector2 pos, float depth = 5)
    {
        Vector2 posCamDiff = Vector2.LerpUnclamped(new(0.5f, 0.5f), pos, Options.ConvergenceScale) - this._camPos;
        if (!Options.DynamicOptimization)
        {
            posCamDiff.x = Mathf.Clamp(posCamDiff.x, -Options.MaxWarp, Options.MaxWarp);
            posCamDiff.y = Mathf.Clamp(posCamDiff.y, -Options.MaxWarp, Options.MaxWarp);
        }
        float d = depth >= 30 ? 1 : DepthCurve(depth / 30.0f) / Options.BackgroundDepth;
        return this.currentWarp * (Options.PivotDepth - d) * posCamDiff; //1 - d, because d=1 => no warp, but d=0 => full warp
    }

    public float DepthCurve(float d) => Options.DepthCurve switch
        {
            Options.DepthCurveOptions.EXTREME => d * (d * (d - 3) + 3),
            Options.DepthCurveOptions.PARABOLIC => d * (2 - d),
            Options.DepthCurveOptions.INVERSE => d * d,
            Options.DepthCurveOptions.REALISTIC => 1 - 1.0f / (6*d * Mathf.Abs(currentWarp)/Custom.rainWorld.screenSize.x + 1),
            _ => d //LINEAR
        };
}
