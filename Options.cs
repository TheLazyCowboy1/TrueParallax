using EasyModSetup;
using Menu.Remix.MixedUI;
using System;
using System.Linq;

namespace TrueParallax;

public class Options : AutoConfigOptions
{
    private const string BASICS = "Basics";
    private const string CAMERA = "Camera";
    private const string LAYER2 = "Layer2";
    private const string OPTIMIZATION = "Optimization";
    private const string ADVANCED = "Advanced";

    public Options() : base(new TabInfo[]
    {
        new(BASICS) { spacing = 40 },
        new(CAMERA),
        new(LAYER2),
        new(OPTIMIZATION),
        new(ADVANCED)
    })
    {
    }

    //BASICS
    [Config(BASICS, "Effect Strength", "How strong the parallax effect is. Higher numbers will decrease performance and make the camera more zoomed in.\nRecommended between 30 and 100.", spaceAfter = 20, precision = 1), LimitRange(-500, 500)]
    public static float Warp = 50;

    [Config(BASICS, "Limit Projection", "Limits the thickness of objects like poles and creatures, but at a slight performance cost.\nHIGHLY recommended, because otherwise creatures look very stretched.")]
    public static bool LimitProjection = true;
    [Config(BASICS, "Max Projection", "How thick poles and creatures appear. Setting this too low will make geometry look disconnected, but setting it too high makes creatures and poles still look stretched.\nRecommended between 0.04 and 0.10. 0 = everything is paper-thin; 1 = everything is fully stretched.", spaceAfter = 20), LimitRange(0, 1)]
    public static float MaxProjection = 0.1f;

    [Config(BASICS, "Second Layer", "Makes the shader much better at determining how thick to make objects like poles and creatures, at the cost of performance.\nSee the "+LAYER2+" tab for more advanced control of this feature.")]
    public static bool TwoLayers = false;

    //CAMERA

    [Config(CAMERA, "Camera Move Speed", "How smoothly the camera follows the player. Lower values mean smoother movements, but high values can make it feel too snappy.\nRecommended between 0.07 and 0.20. 0 = no movement; 1 = no smoothing."), LimitRange(0, 1)]
    public static float CameraMoveSpeed = 0.1f;
    [Config(CAMERA, "Always Centered", "Locks the camera in the middle of the screen, not following the player at all.\nRecommended if you are using SBCameraScroll and experience motion-sickness. Otherwise, keep this setting OFF!")]
    public static bool AlwaysCentered = false;
    [Config(CAMERA, "Transitions Reset Camera", "Instantly snaps the camera into place whenever going through screen transitions. If disabled, the camera will often pan across the entire screen upon screen transitions.\nHIGHLY recommended, especially if you are prone to motion-sickness. But personally, I think it looks cool when this option is disabled.")]
    public static bool TransitionsResetCamera = true;

    [Config(CAMERA, "Mouse Sensitivity", "How much the camera moves when the mouse is moved. If 0, mouse movement does not affect the camera.", precision = 1), LimitRange(-10, 10)]
    public static float MouseSensitivity = 0;

    [Config(CAMERA, "Invert Position", "Makes the camera think the player is on the opposite end of the room; thus, camera motion is opposite player motion.\nNOT recommended.")]
    public static bool InvertPos = false;
    [Config(CAMERA, "Dynamic Zoom", "How much the camera zooms out when moving towards the center of the screen.\nNOT recommended; keep at 0."), LimitRange(0, 1)]
    public static float DynamicZoom = 0;

    //LAYER2

    [Config(LAYER2, "Min Object Thickness", "The minimum thickness of geometry like poles. Setting this too low can make geometry appear disconnected.\nRecommended between 1 and 3."), LimitRange(0, 30)]
    public static float MinObjectThickness = 2;
    [Config(LAYER2, "Thickness Modifier", "How much thicker wide objects (like pipes) are than narrow ones (like poles).\nRecommended between 0.5 and 1."), LimitRange(0, 3)]
    public static float ThicknessMod = 0.65f;

    [Config(LAYER2, "Simpler Layers", "Reduces the lag when changing screens, but loses some finer details in the process.\nRecommended if you notice lag upon screen transitions.")]
    public static bool SimplerLayers = false;
    [Config(LAYER2, "Cached Textures", "How many Layer2 textures are saved. Saves processing when going back to previous screens, at the cost of VRAM.\nRecommended at 1 or 2. Any higher is usually useless."), LimitRange(1, 8)]
    public static int CachedRenderTextures = 2;

    //OPTIMIZATION

    [Config(OPTIMIZATION, "Optimization", "Reduces processing costs, at the risk of visual artefacts due to \"skipping over\" pixels.\nRecommended between 1 and 2. A good compromise is 1.5."), LimitRange(0.25f, 4)]
    public static float OptimizationFac = 1;
    [Config(OPTIMIZATION, "Dynamic Optimization", "Reduces processing costs for pixels closer to the camera (by about 50% on average), but can cause some visual artefacts.\nRecommended to EITHER set Optimization to 1.5, OR enable this and use Max Warp.")]
    public static bool DynamicOptimization = false;
    [Config(OPTIMIZATION, "Max Warp", "Caps the strength of the parallax effect, only affecting the further parts of the screen.\nIF using Dynamic Optimization, recommended between 0.6 and 0.9. OTHERWISE, keep at 1."), LimitRange(0, 1)]
    public static float MaxWarp = 1;

    //ADVANCED

    [Config(ADVANCED, "Background Noise", "Applies noise to areas that look stretched. There is a performance benefit if this is 0.\nRecommended value = 1."), LimitRange(0, 4)]
    public static float BackgroundNoise = 1;
    [Config(ADVANCED, "Anti-Aliasing", "Attempts to break up straight lines that are noticable when moving the camera slowly. (Not really anti-aliasing).\nRecommended below 0.5."), LimitRange(0, 1)]
    public static float AntiAliasing = 0.1f;

    public enum DepthCurveOptions { EXTREME, PARABOLIC, LINEAR, INVERSE };
    [Config(ADVANCED, "Depth Curve", "Applies a curve to the room depth - for example, making mid-ground objects appear further away.\nLINEAR recommended. PARABOLIC may be useful if you need a low Effect Strength due to low processing power.", width = 120, spaceAfter = 200)]
    public static DepthCurveOptions DepthCurve = DepthCurveOptions.LINEAR;

    [Config(ADVANCED, "Background Depth", "How far away the background (the sky, basically) appears relative to the room geometry. Literally decreases the Effect Strength for everything except the background.\nHIGHLY recommended at 1, because the background is usually a mostly solid color, making this just a waste of resources."), LimitRange(1, 2)]
    public static float BackgroundDepth = 1; //1.0 / Layer30Depth
    [Config(ADVANCED, "Pivot Depth", "What depth stays fixed in place. Decreasing this decreases zoom and causes an inverse parallax effect, where the background moves but the foreground does not.\nHIGHLY recommended at 1, because lower values look weird."), LimitRange(0, 1)]
    public static float PivotDepth = 1;
    [Config(ADVANCED, "Convergence Scale", "Essentially how \"zoomed in\" the camera appears.\nHIGHLY recommended at 1, because lower values cause black bars on the side, and higher values feel like a waste of resources."), LimitRange(-5, 5)]
    public static float ConvergenceScale = 1;

    [Config(ADVANCED, "Log Level", "When this number is higher, less important logs are displayed."), LimitRange(0, 3)]
    public static int LogLevel = 1;


    public override void Update()
    {
        base.Update();

        try
        {
            (ConfigUIs[nameof(MaxProjection)] as UIfocusable).greyedOut = !LimitProjection;
            (ConfigUIs[nameof(CameraMoveSpeed)] as UIfocusable).greyedOut = AlwaysCentered;

            OpTab layer2 = Tabs.FirstOrDefault(t => t.name == LAYER2);
            if (layer2 != null)
            {
                foreach (UIelement el in layer2.items)
                {
                    if (el is UIfocusable foc) foc.greyedOut = !TwoLayers;
                }
            }
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

}
