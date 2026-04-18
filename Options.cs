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
        new(BASICS) { spacing = 40, startHeight = 500 },
        new(CAMERA),
        new(LAYER2) { startHeight = 500 },
        new(OPTIMIZATION),
        new(ADVANCED)
    })
    {
    }

    //BASICS
    [Config(BASICS, "Effect Strength", "How strong the parallax effect is. Higher numbers will decrease performance and make the camera more zoomed in.\nRecommended between 50 and 200.", spaceAfter = 20, precision = 1), LimitRange(-1000, 1000)]
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
    [Config(CAMERA, "Movement Stop Distance", "If the camera's distance from its target position is less than this distance, then it does not try to move any closer. Measured as a fraction of the screen size.\nRecommended between 0.002 and 0.01. 0 = camera always tries to inch closer; 1 = camera never moves.", precision = 3), LimitRange(0, 1)]
    public static float CameraStopDistance = 0.005f;

    [Config(CAMERA, "Input Offset", "How much the camera position moves according to the player's inputs.\nGenerally recommended, unless you have a high Camera Move Speed.", precision = 1), LimitRange(-500, 500)]
    public static float CameraInputOffset = 50;

    [Config(CAMERA, "Always Centered", "Locks the camera in the middle of the screen, not following the player at all.\nRecommended if you are using SBCameraScroll and experience motion-sickness. Otherwise, keep this setting OFF!")]
    public static bool AlwaysCentered = false;
    [Config(CAMERA, "Transitions Reset Camera", "Instantly snaps the camera into place whenever going through screen transitions. If disabled, the camera will often pan across the entire screen upon screen transitions.\nHIGHLY recommended, especially if you are prone to motion-sickness. But personally, I think it looks cool when this option is disabled.")]
    public static bool TransitionsResetCamera = true;

    [Config(CAMERA, "Mouse Sensitivity", "How much the camera moves when the mouse is moved. If 0, mouse movement does not affect the camera.", spaceBefore = 15), LimitRange(-5, 5)]
    public static float MouseSensitivity = 0;

    [Config(CAMERA, "Shift Background Scenes", "Shifts the position of background scenes (like the views above the clouds) slightly to match the movement of the camera.\nRecommended at 0. 1 = background follows player movement; -1 = follows back wall's movement.", spaceBefore = 15), LimitRange(-5, 5)]
    public static float BackgroundShift = 0;

    [Config(CAMERA, "Invert Position", "Makes the camera think the player is on the opposite end of the room; thus, camera motion is opposite player motion.\nNOT recommended.", spaceBefore = 15)]
    public static bool InvertPos = false;
    [Config(CAMERA, "Dynamic Zoom", "How much the camera zooms out when moving towards the center of the screen.\nNOT recommended; keep at 0. 0 = zoom remains constant; 1 = the parallax effect is entirely disabled when standing in the center of the screen."), LimitRange(0, 1)]
    public static float DynamicZoom = 0;

    //LAYER2

    [Config(LAYER2, "Min Object Thickness", "The minimum thickness of geometry like poles. Setting this too low can make geometry appear disconnected.\nRecommended between 1 and 3."), LimitRange(0, 30)]
    public static float MinObjectThickness = 2;
    [Config(LAYER2, "Thickness Modifier", "How much thicker wide objects (like pipes) are than narrow ones (like poles).\nRecommended between 0.5 and 1. 0 = everything is the same thickness."), LimitRange(0, 3)]
    public static float ThicknessMod = 0.65f;
    [Config(LAYER2, "Max Depth Difference", "How severe background interpolation can be. Basically, if this number is too high, things can look stretched; but if it is too low, backgrounds look less smooth.\nRecommended between 0.5 and 1. 0 = no background interpolation; 10 = floating bits in background."), LimitRange(0, 10)]
    public static float MaxDepthDifference = 1;
    [Config(LAYER2, "Sample Count", "How many texture samples are performed when determining the background. Currently cannot exceed 31 due to being only 5-bit.\nRecommended above 20, because 20 = 1 tile."), LimitRange(1, 31)]
    public static int BackgroundTestNum = 22;

    [Config(LAYER2, "Simpler Layers", "Reduces the lag when changing screens, but loses some finer details in the process. Specifically, halves the number of texture samples.\nRecommended if you notice lag upon screen transitions.", spaceBefore = 15)]
    public static bool SimplerLayers = false;
    [Config(LAYER2, "Cached Textures", "How many Layer2 textures are saved. Saves processing when going back to previous screens, at the cost of VRAM.\nRecommended at 2. Any higher is usually useless."), LimitRange(1, 8)]
    public static int CachedRenderTextures = 2;

    [Config(LAYER2, "Build Creature Backgrounds", "Attempts to infer the room geometry behind creatures by checking the pixels around them. Has a significant performance cost. Also applies to dynamic level elements.\nRecommended if you are using a high Effect Strength and have a stable framerate.", spaceBefore = 15)]
    public static bool BuildCreatureBackground = false;
    [Config(LAYER2, "Creature Background Samples", "How many pixels around the creature are checked. Will affect performance.\nRecommended below 20."), LimitRange(1, 31)]
    public static int CreatureBackgroundTests = 10;

    [Config(LAYER2, "Fallback Thickness", "Used for dynamic level elements like turbines and floating rocks (in the Watcher campaign). Otherwise, this is not used.\nRecommended between 2 and 10.", spaceBefore = 15), LimitRange(1, 31)]
    public static int DefaultLevelThickness = 5;

    //OPTIMIZATION

    [Config(OPTIMIZATION, "Optimization", "Reduces processing costs, at the risk of visual artefacts due to \"skipping over\" pixels.\nRecommended between 1 and 2. A good compromise is 1.5."), LimitRange(0.25f, 4)]
    public static float OptimizationFac = 1;
    [Config(OPTIMIZATION, "Dynamic Optimization", "Reduces processing costs for pixels closer to the camera (by about 50% on average), but can cause some minor visual artefacts (serrated edges, pixelated backgrounds).\nRecommended to EITHER set Optimization to 1.5, OR enable this and use Max Warp.")]
    public static bool DynamicOptimization = false;
    [Config(OPTIMIZATION, "Max Warp", "Caps the strength of the parallax effect, only affecting the further parts of the screen. This can significantly improve performance.\nIF using Dynamic Optimization, recommended between 0.5 and 0.8. OTHERWISE, keep above 0.8."), LimitRange(0, 1)]
    public static float MaxWarp = 1;

    //ADVANCED

    [Config(ADVANCED, "Background Noise", "Applies noise to areas that look stretched. There is a performance benefit if this is 0.\nRecommended between 0.2 and 0.8."), LimitRange(0, 4)]
    public static float BackgroundNoise = 0.5f;
    [Config(ADVANCED, "Anti-Aliasing", "Attempts to break up straight lines that are noticable when moving the camera slowly. (Not really anti-aliasing). Has a minimal effect when the Effect Strength is high.\nRecommended below 1. May be useful when Dynamic Optimization is enabled."), LimitRange(0, 10)]
    public static float AntiAliasing = 0.1f;

    public enum DepthCurveOptions { INVERSE, LINEAR, PARABOLIC, CUBIC, REALAPPROX, REALISTIC };
    [Config(ADVANCED, "Depth Curve", "Applies a curve to the room depth - for example, making mid-ground objects appear farther. INVERSE = mid-ground looks closer; PARABOLIC, CUBIC, REALAPPROX = mid-ground appears farther; REALISTIC = mathematically accurate proportions.\nLINEAR or PARABOLIC recommended. REALAPPROX is NOT recommended for high or low Effect Strengths. REALISTIC is NOT recommended due to being extremely expensive.", width = 120, spaceAfter = 100)]
    public static DepthCurveOptions DepthCurve = DepthCurveOptions.LINEAR;

    [Config(ADVANCED, "Super Accurate Thickness", "Ensures that the depth curve applies properly to geometry thickness. Adds additional performance cost for a very tiny visual improvement. Does not work for LINEAR or PARABOLIC.\nNOT recommended: The improvement is not worth the cost. This is most useful with the REALISTIC depth curve, but it is also very expensive with that curve.")]
    public static bool SuperAccurateThickness = false;
    [Config(ADVANCED, "Background Depth", "How far away the background (the sky, basically) appears relative to the room geometry. Literally decreases the Effect Strength for everything except the background.\nRecommended at 1, because the background is usually a solid color, making this just a waste of resources (although For Scenes Only helps with this).", spaceBefore = 40), LimitRange(1, 2)]
    public static float BackgroundDepth = 1; //1.0 / Layer30Depth
    [Config(ADVANCED, "For Scenes Only", "Sets Background Depth to 1 EXCEPT when a Background Scene (e.g: AboveCloudsView, RoofTopView) is active in the room.\nRecommended for performance reasons. This only applies when Background Depth is > 1.", rightSide = true)]
    public static bool BackDepthForScenesOnly = true;

    [Config(ADVANCED, "Pivot Depth", "What depth stays fixed in place. Decreasing this decreases zoom and causes an inverse parallax effect, where the background moves but the foreground does not.\nHIGHLY recommended at 1, because lower values look weird. However, this is the best way to make the game look less zoomed-in."), LimitRange(0, 1)]
    public static float PivotDepth = 1;
    [Config(ADVANCED, "Convergence Scale", "Essentially how \"zoomed in\" the camera appears.\nHIGHLY recommended at 1, because lower values cause black bars on the side, and higher values feel like a waste of resources."), LimitRange(-5, 5)]
    public static float ConvergenceScale = 1;
    [Config(ADVANCED, "General Scale", "Scales the image by multiplying the uv coordinates. Higher scales make things look bigger.\nHIGHLY recommended at 1."), LimitRange(0.1f, 10)]
    public static float GeneralScale = 1;

    [Config(ADVANCED, "Log Level", "When this number is higher, less important logs are logged to the LogOutput.log file.", spaceBefore = 10), LimitRange(0, 3)]
    public static int LogLevel = 1;


    private class OptimizationLabel : OpLabelLong
    {
        private readonly record struct MyBools {
            public readonly bool dynamicOptimization = !Options.DynamicOptimization,
                secondLayer = Options.TwoLayers,
                limitProjection = Options.LimitProjection,
                backgroundNoise = Options.BackgroundNoise > 0,
                buildCreatureBackgrounds = Options.TwoLayers && Options.BuildCreatureBackground,
                realisticDepthCurve = Options.DepthCurve == DepthCurveOptions.REALISTIC,
                superAccurateThickness = Options.SuperAccurateThickness && (Options.LimitProjection || Options.TwoLayers) && Options.DepthCurve != DepthCurveOptions.LINEAR && Options.DepthCurve != DepthCurveOptions.PARABOLIC;
            public MyBools() { }
        }
        private MyBools myBools = new();
        public OptimizationLabel(float x, float y) : base(new(x, y), new(500, 150), "PLACEHOLDER")
        {
            UpdateText();
        }

        private void UpdateText()
        {
            this.text =
                "If you want to know how expensive the shader is, use this basic formula:\n" +
                "cost = EffectStrength * MaxWarp / Optimization\n" +
                "Thus, Effect Strength is the primary factor for performance cost, and Max Warp and Optimization are used to directly reduce it.\n" +
                "\nOther optimizations:"
                + (myBools.dynamicOptimization ? "\n* Enabling Dynamic Optimization improves performance by roughly 50%." : "")
                + (myBools.secondLayer ? "\n* Disabling Second Layer could improve performance by 50-100%, because it is highly expensive." : "")
                + (myBools.limitProjection ? "\n* Disabling Limit Projection could improve performance by up to 50%; but I recommend keeping it on anyway." : "")
                + (myBools.backgroundNoise ? "\n* Setting Background Noise to 0 should improve performance by perhaps 10% (exact improvement is untested)." : "")
                + (myBools.buildCreatureBackgrounds ? "\n* Disabling Build Creature Backgrounds or reducing Creature Background Samples will help. 1 CreatureBackgroundSample is worth about 3 EffectStrength" : "")
                + (myBools.realisticDepthCurve ? "\n* Use a Depth Curve other than REALISTIC. It involves repeated divisions, which is highly expensive." : "")
                + (myBools.superAccurateThickness ? "\n* Disabling Super Accurate Thickness should improve performance some. I have not determined the exact improvement." : "")
                ;
        }

        public override void Update()
        {
            base.Update();
            MyBools newBools = new();
            if (newBools != myBools)
            {
                myBools = newBools;
                UpdateText();
                Plugin.Log("Updated Optimization Label text", 3);
            }
        }
    }

    OpLabel layer2Label;
    public override void MenuInitialized()
    {
        base.MenuInitialized();

        GetTab(BASICS).AddItems(
            new OpLabel(50, 550, "Parallax Effect Settings", true)
            );

        GetTab(LAYER2).AddItems(
            layer2Label = new(50, 550, $"Enable \"Second Layer\" in {BASICS} tab to configure these settings.")
            );
        layer2Label.OnReactivate += () => layer2Label.Hidden = false; //trying to fix bug where it shows up sometimes when opening config menu

        GetTab(OPTIMIZATION).AddItems(
            new OptimizationLabel(50, 200)
            );
    }

    public override void Update()
    {
        base.Update();

        try
        {
            UIConfigs[nameof(MaxProjection)].greyedOut = !LimitProjection;
            UIConfigs[nameof(CameraMoveSpeed)].greyedOut = AlwaysCentered;
            UIConfigs[nameof(CameraStopDistance)].greyedOut = AlwaysCentered;
            UIConfigs[nameof(BackDepthForScenesOnly)].greyedOut = BackgroundDepth <= 1;
            UIConfigs[nameof(SuperAccurateThickness)].greyedOut = (DepthCurve == DepthCurveOptions.LINEAR || DepthCurve == DepthCurveOptions.PARABOLIC) || !(Options.LimitProjection || Options.TwoLayers);

            OpTab layer2 = Tabs.FirstOrDefault(t => t.name == LAYER2);
            if (layer2 != null)
            {
                foreach (UIelement el in layer2.items)
                {
                    if (el is UIconfig cfg) cfg.greyedOut = !TwoLayers;
                }
            }
            layer2Label.Hidden = TwoLayers;

            UIConfigs[nameof(CreatureBackgroundTests)].greyedOut = !TwoLayers || !BuildCreatureBackground;
            UIConfigs[nameof(DefaultLevelThickness)].greyedOut = !TwoLayers || BuildCreatureBackground;
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

}
