using System;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using EasyModSetup;
using UnityEngine;
using RWCustom;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TrueParallax;

[BepInDependency("SBCameraScroll", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.henpemaz.splitscreencoop", BepInDependency.DependencyFlags.SoftDependency)]

[BepInPlugin("LazyCowboy.TrueParallax", "True Parallax", "0.0.1")]
public partial class Plugin : SimplerPlugin
{

    #region Setup
    public override int LogLevel => Options.LogLevel;

    public Plugin() : base(new Options())
    {
    }

    #endregion

    #region Initialization

    public static bool SBCameraScrollEnabled = false;
    public static bool SplitScreenEnabled = false;

    public override void ModsApplied()
    {
        base.ModsApplied();

        SBCameraScrollEnabled = ModManager.ActiveMods.Any(m => m.id == "SBCameraScroll");
        SplitScreenEnabled = ModManager.ActiveMods.Any(m => m.id == "henpemaz_splitscreencoop");

        LoadAssets();

        ShadPropCamPos = Shader.PropertyToID("LZC_CamPos");
        ShadPropWarp = Shader.PropertyToID("LZC_Warp");
        ShadPropTestNum = Shader.PropertyToID("LZC_TestNum");
        ShadPropStepSize = Shader.PropertyToID("LZC_StepSize");
        ShadPropMoveStepScale = Shader.PropertyToID("LZC_MoveStepScale");
        ShadPropLayer2Tex = Shader.PropertyToID("_LZC_Layer2Tex");

        RemoveLevelHeatAndMelt();
    }

    /// <summary>
    /// Index of a shader variable (e.g: LZC_CamPos), used for presumably more efficient access to it
    /// </summary>
    public static int ShadPropCamPos = -1, ShadPropWarp = -1,
        ShadPropTestNum = -1, ShadPropStepSize = -1,
        ShadPropMoveStepScale = -1, ShadPropLayer2Tex = -1;

    public static FShader TrueParallaxFShader;
    public static Material ThicknessMapMaterial;

    public static void LoadAssets()
    {
        try
        {
            AssetBundle assetBundle = AssetBundle.LoadFromFile(AssetManager.ResolveFilePath("AssetBundles\\ParallaxEffect.assets"));

            //load true parallax shader
            Shader TrueParallaxShader = assetBundle.LoadAsset<Shader>("TrueParallax.shader");
            if (TrueParallaxShader == null)
                Error("Could not find shader TrueParallax.shader");
            TrueParallaxFShader = FShader.CreateShader("LZC_TrueParallax", TrueParallaxShader);

            Shader ThicknessMapShader = assetBundle.LoadAsset<Shader>("ThicknessMap.shader");
            if (ThicknessMapShader == null)
                Error("Could not find shader ThicknessMap.shader");
            ThicknessMapMaterial = new(ThicknessMapShader);

        }
        catch (Exception ex) { Error(ex); }
    }

    /// <summary>
    /// The LevelHeat and LevelMelt shaders are especially problematic, because they warp the level itself
    /// which causes visual artefacts due _LevelTex not matching up with the drawn room.
    /// </summary>
    public static void RemoveLevelHeatAndMelt()
    {
        try
        {
            Custom.rainWorld.Shaders["LevelHeat"].keywords = null;
            Custom.rainWorld.Shaders["LevelMelt"].keywords = null;
            Log("Cleared keywords for LevelHeat and LevelMelt shaders");
        }
        catch (Exception ex) { Error(ex); }
    }

    #endregion

    #region HookSetup

    public override void ApplyHooks()
    {
        //CameraSetupHooks.cs
        On.RoomCamera.ctor += RoomCamera_ctor;

        //CameraMovementHooks.cs
        On.RoomCamera.DrawUpdate += RoomCamera_DrawUpdate;
        On.RoomCamera.Update += RoomCamera_Update;

        //RoomEffectHooks.cs
        On.RoomCamera.MoveCamera_Room_int += RoomCamera_MoveCamera_Room_int;
        On.RoomCamera.WarpMoveCameraActual += RoomCamera_WarpMoveCameraActual;
        On.RoomCamera.ApplyPalette += RoomCamera_ApplyPalette;

        //BackgroundHooks.cs
        On.BackgroundScene.DrawPos += BackgroundScene_DrawPos;
        On.Watcher.OuterRimView.DrawPos += OuterRimView_DrawPos;

        //Miscellaneous
        On.RoomCamera.ApplyPositionChange += RoomCamera_ApplyPositionChange;

        On.RoomCamera.ClearAllSprites += RoomCamera_ClearAllSprites;
    }

    public override void RemoveHooks()
    {
        On.RoomCamera.ctor -= RoomCamera_ctor;
        On.RoomCamera.DrawUpdate -= RoomCamera_DrawUpdate;
        On.RoomCamera.Update -= RoomCamera_Update;

        On.RoomCamera.MoveCamera_Room_int -= RoomCamera_MoveCamera_Room_int;
        On.RoomCamera.WarpMoveCameraActual -= RoomCamera_WarpMoveCameraActual;
        On.RoomCamera.ApplyPalette -= RoomCamera_ApplyPalette;

        On.BackgroundScene.DrawPos -= BackgroundScene_DrawPos;
        On.Watcher.OuterRimView.DrawPos -= OuterRimView_DrawPos;

        On.RoomCamera.ApplyPositionChange -= RoomCamera_ApplyPositionChange;

        On.RoomCamera.ClearAllSprites -= RoomCamera_ClearAllSprites;
    }


    public const string PARALLAXCONTAINER = "PARALLAX";//"HUD";

    public static RenderTexture ScreenLevelTex;

    #endregion

    #region MiscHooks

    //Sets up layer2
    private void RoomCamera_ApplyPositionChange(On.RoomCamera.orig_ApplyPositionChange orig, RoomCamera self)
    {
        orig(self);

        try
        {
            if (!self.TryGetData(out CameraData data)) return;

            if (Options.TransitionsResetCamera)
                data.CamPos = new(-1, -1); //don't lerp from previous position

            if (Options.TwoLayers)
            {
                data.layer2Textures.Resize(Options.CachedRenderTextures); //ensure it's the right size

                RenderTexture tex = data.layer2Textures.GetOrCreateTexture();
                data.SpriteMaterial?.SetTexture(ShadPropLayer2Tex, tex);
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    //Clear data, just to be sure
    private void RoomCamera_ClearAllSprites(On.RoomCamera.orig_ClearAllSprites orig, RoomCamera self)
    {
        orig(self);

        try
        {
            if (self.TryGetData(out CameraData data))
            {
                data.Clear();
                Log("Cleared data for camera#" + self.cameraNumber);
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    #endregion

}
