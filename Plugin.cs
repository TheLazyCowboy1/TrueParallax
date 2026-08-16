using System;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using EasyModSetup;
using UnityEngine;
using RWCustom;
using TrueParallax.ModCompat;
using Unity.Mathematics;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TrueParallax;

[BepInDependency("SBCameraScroll", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.henpemaz.splitscreencoop", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("pjb3005.sharpener", BepInDependency.DependencyFlags.SoftDependency)]

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
    public static bool SharpenerEnabled = false;

    /// <summary>
    /// Index of a shader variable (e.g: LZC_CamPos), used for presumably more efficient access to it
    /// </summary>
    public static int ShadPropCamPos = -1, ShadPropWarp = -1,
        ShadPropTestNum = -1, ShadPropStepSize = -1,
        ShadPropMoveStepScale = -1, ShadPropLayer2Tex = -1,
        ShadPropLevelHeatAmount = -1, ShadPropMaxProjection = -1,
        ShadPropUVOffset = -1;

    public override void ModsApplied()
    {
        base.ModsApplied();

        SBCameraScrollEnabled = ModManager.ActiveMods.Any(m => m.id == "SBCameraScroll");
        SplitScreenEnabled = ModManager.ActiveMods.Any(m => m.id == "henpemaz_splitscreencoop");
        SharpenerEnabled = ModManager.ActiveMods.Any(m => m.id == "pjb3005.sharpener");

        LoadAssets();

        ShadPropCamPos = Shader.PropertyToID("LZC_CamPos");
        ShadPropWarp = Shader.PropertyToID("LZC_Warp");
        ShadPropTestNum = Shader.PropertyToID("LZC_TestNum");
        ShadPropStepSize = Shader.PropertyToID("LZC_StepSize");
        ShadPropMoveStepScale = Shader.PropertyToID("LZC_MoveStepScale");
        ShadPropLayer2Tex = Shader.PropertyToID("_LZC_Layer2Tex");
        ShadPropLevelHeatAmount = Shader.PropertyToID("LZC_LevelHeatAmount");
        ShadPropMaxProjection = Shader.PropertyToID("LZC_MaxProjection");
        ShadPropUVOffset = Shader.PropertyToID("LZC_UVOffset");

        RemoveLevelHeatAndMelt();
    }

    public static FShader TrueParallaxFShader;
    public static Material ThicknessMapMaterial;
    public static Shader CustomBlendShader;
    public static FShader CustomSkyBloomFShader;

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

            //motion blur stuff?
            CustomBlendShader = assetBundle.LoadAsset<Shader>("CustomBlend.shader");
            if (CustomBlendShader == null)
                Error("Could not find shader CustomBlend.shader");
            else
            {
                Futile.instance.camera.gameObject.AddComponent<MotionBlur>();
                Log("Attached MotionBlur MonoBehaviour to camera");
            }

            //replace water shader
            Shader DeepWater = assetBundle.LoadAsset<Shader>("DeepWater.shader");
            if (DeepWater == null)
                Error("Could not find shader DeepWater.shader");
            else
            {
                FShader._shaders.Find(s => s.name == "DeepWater" || s.name.EndsWith("/DeepWater")).shader = DeepWater;
            }

            //replace LightBloom shader
            Shader LightBloom = assetBundle.LoadAsset<Shader>("LightBloom.shader");
            if (LightBloom == null)
                Error("Could not find shader LightBloom.shader");
            else
            {
                FShader._shaders.Find(s => s.name == "LightBloom" || s.name.EndsWith("/LightBloom")).shader = LightBloom;
            }

            //custom sky bloom
            Shader NewSkyBloom = assetBundle.LoadAsset<Shader>("NewSkyBloom.shader");
            if (NewSkyBloom == null)
                Error("Could not find shader NewSkyBloom.shader");
            CustomSkyBloomFShader = FShader.CreateShader("LZC_NewSkyBloom", NewSkyBloom);

            return;

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
        IL.RoomCamera.DrawUpdate += IL_RoomCamera_DrawUpdate;

        //RoomEffectHooks.cs
        On.RoomCamera.MoveCamera_Room_int += RoomCamera_MoveCamera_Room_int;
        On.RoomCamera.WarpMoveCameraActual += RoomCamera_WarpMoveCameraActual;
        On.RoomCamera.ApplyPalette += RoomCamera_ApplyPalette;

        On.RoomRain.AddToContainer += RoomRain_AddToContainer;
        On.MoreSlugcats.CellDistortion.InitiateSprites += CellDistortion_InitiateSprites;
        On.CustomDecal.GetIdealGridDiv += CustomDecal_GetIdealGridDiv;
        On.CustomDecal.UpdateVerts += CustomDecal_UpdateVerts;
        On.GateKarmaGlyph.InitiateSprites += GateKarmaGlyph_InitiateSprites;
        On.Watcher.WeaverThread.InitiateSprites += WeaverThread_InitiateSprites;

        //BackgroundHooks.cs
        On.BackgroundScene.DrawPos += BackgroundScene_DrawPos;
        On.Watcher.OuterRimView.DrawPos += OuterRimView_DrawPos;
        On.RotWormScene.DrawPos += RotWormScene_DrawPos;

        On.AboveCloudsView.CloseCloud.DrawSprites += CloseCloud_DrawSprites;
        On.AboveCloudsView.DistantCloud.DrawSprites += DistantCloud_DrawSprites;
        On.AboveCloudsView.FlyingCloud.DrawSprites += FlyingCloud_DrawSprites;
        On.RoofTopView.Floor.DrawSprites += Floor_DrawSprites;
        //On.RoofTopView.DustWave.DrawSprites += DustWave_DrawSprites;
        On.RoofTopView.Rubble.DrawSprites += Rubble_DrawSprites;
        On.BackgroundScene.Simple2DBackgroundIllustration.DrawSprites += Simple2DBackgroundIllustration_DrawSprites;

        //Miscellaneous
        On.RoomCamera.ApplyPositionChange += RoomCamera_ApplyPositionChange;

        On.RoomCamera.ClearAllSprites += RoomCamera_ClearAllSprites;

        if (SBCameraScrollEnabled)
            SBCameraScrollMod.ApplyHooks();

    }

    private void WeaverThread_InitiateSprites(On.Watcher.WeaverThread.orig_InitiateSprites orig, Watcher.WeaverThread self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        try
        {
            self.AddToContainer(sLeaser, rCam, rCam.ReturnFContainer(AFTERPARALLAXCONTAINER));
        } catch (Exception ex) { Error(ex); }
    }

    private void GateKarmaGlyph_InitiateSprites(On.GateKarmaGlyph.orig_InitiateSprites orig, GateKarmaGlyph self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        try
        {
            FContainer container = rCam.ReturnFContainer(AFTERPARALLAXCONTAINER);
            for (int i = 1; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                container.AddChild(sLeaser.sprites[i]);
            }
        } catch (Exception ex) { Error(ex); }
    }

    public override void RemoveHooks()
    {
        On.RoomCamera.ctor -= RoomCamera_ctor;

        On.RoomCamera.DrawUpdate -= RoomCamera_DrawUpdate;
        On.RoomCamera.Update -= RoomCamera_Update;
        IL.RoomCamera.DrawUpdate -= IL_RoomCamera_DrawUpdate;

        On.RoomCamera.MoveCamera_Room_int -= RoomCamera_MoveCamera_Room_int;
        On.RoomCamera.WarpMoveCameraActual -= RoomCamera_WarpMoveCameraActual;
        On.RoomCamera.ApplyPalette -= RoomCamera_ApplyPalette;

        On.RoomRain.AddToContainer -= RoomRain_AddToContainer;
        On.MoreSlugcats.CellDistortion.InitiateSprites -= CellDistortion_InitiateSprites;
        On.CustomDecal.GetIdealGridDiv -= CustomDecal_GetIdealGridDiv;
        On.CustomDecal.UpdateVerts -= CustomDecal_UpdateVerts;

        On.BackgroundScene.DrawPos -= BackgroundScene_DrawPos;
        On.Watcher.OuterRimView.DrawPos -= OuterRimView_DrawPos;
        On.RotWormScene.DrawPos -= RotWormScene_DrawPos;

        On.AboveCloudsView.CloseCloud.DrawSprites -= CloseCloud_DrawSprites;
        On.AboveCloudsView.DistantCloud.DrawSprites -= DistantCloud_DrawSprites;
        On.AboveCloudsView.FlyingCloud.DrawSprites -= FlyingCloud_DrawSprites;
        On.RoofTopView.Floor.DrawSprites -= Floor_DrawSprites;
        //On.RoofTopView.DustWave.DrawSprites -= DustWave_DrawSprites;
        On.RoofTopView.Rubble.DrawSprites -= Rubble_DrawSprites;
        On.BackgroundScene.Simple2DBackgroundIllustration.DrawSprites -= Simple2DBackgroundIllustration_DrawSprites;

        On.RoomCamera.ApplyPositionChange -= RoomCamera_ApplyPositionChange;

        On.RoomCamera.ClearAllSprites -= RoomCamera_ClearAllSprites;

        if (SBCameraScrollEnabled)
            SBCameraScrollMod.RemoveHooks();
    }

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

            if (Options.DynamicAdjustmentThreshold > 0)
            {
                int fpsCap = Custom.rainWorld.options.fpsCap;
                float targetFrameRate = Mathf.Min(Options.DynamicAdjustmentThreshold, fpsCap < 1 ? 300 : fpsCap * 0.75f); //don't penalize for being under 75% of fpsCap
                float warpScale = 1.0f / (Mathf.Clamp(data.averageDeltaTime, 0.0001f, 1) * targetFrameRate); //if deltaTime is too high, decrease warp. If too low, increase
                if (warpScale > 1)
                    warpScale += 0.5f * (warpScale - 1); //increase 50% more quickly than decrease
                warpScale = Mathf.Clamp(warpScale, 0.6f, 1.7f); //arbitrary contraints

                float absWarp = Mathf.Abs(Options.Warp);
                if (warpScale < 1 || Mathf.Abs(data.totalWarp) < absWarp) //don't log when irrelevant
                    Plugin.Log($"Adjusting Warp. warpScale = {warpScale}. old totalWarp = {data.totalWarp}. new totalWarp = {data.totalWarp * warpScale}", 2);

                data.averageDeltaTime *= 0.9f; //decrease averageDeltaTime to make up for the screen transition
                data.totalWarp = Mathf.Clamp(data.totalWarp * warpScale, -absWarp, absWarp); //don't let it exceed the original Warp factor
                data.currentWarp = data.totalWarp;
                SetWarpConstants(data);
            }

            data.layer2Dirty = true;
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
