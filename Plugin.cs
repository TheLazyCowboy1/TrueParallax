using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using BepInEx;
using EasyModSetup;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine;
using RWCustom;
using UnityEngine.Experimental.Rendering;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TrueParallax;

[BepInPlugin("LazyCowboy.TrueParallax", "True Parallax", "0.0.1")]
public class Plugin : SimplerPlugin
{

    #region Setup
    public override int LogLevel => Options.LogLevel;

    public Plugin() : base(new Options())
    {
    }

    #endregion

    #region Initialization

    public static bool SBCameraScrollEnabled = false;

    public override void ModsApplied()
    {
        base.ModsApplied();

        SBCameraScrollEnabled = ModManager.ActiveMods.Any(m => m.id == "SBCameraScroll");

        LoadAssets();

        ShadCamPos = Shader.PropertyToID("LZC_CamPos");
        ShadWarp = Shader.PropertyToID("LZC_Warp");
        ShadTestNum = Shader.PropertyToID("LZC_TestNum");
        ShadStepSize = Shader.PropertyToID("LZC_StepSize");

        RemoveLevelHeatAndMelt();
    }

    /// <summary>
    /// Index of shader variable LZC_CamPos, used for presumably more efficient access to it
    /// </summary>
    public static int ShadCamPos = -1, ShadWarp = -1, ShadTestNum = -1, ShadStepSize = -1;

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

    #region Hooks

    public override void ApplyHooks()
    {
        On.RoomCamera.ctor += RoomCamera_ctor;

        On.RoomCamera.DrawUpdate += RoomCamera_DrawUpdate;
        On.RoomCamera.Update += RoomCamera_Update;

        On.RoomCamera.MoveCamera_Room_int += RoomCamera_MoveCamera_Room_int;
        On.RoomCamera.WarpMoveCameraActual += RoomCamera_WarpMoveCameraActual;

        On.RoomCamera.ApplyPositionChange += RoomCamera_ApplyPositionChange;
    }

    public override void RemoveHooks()
    {
        On.RoomCamera.ctor -= RoomCamera_ctor;
        On.RoomCamera.DrawUpdate -= RoomCamera_DrawUpdate;
        On.RoomCamera.Update -= RoomCamera_Update;

        On.RoomCamera.MoveCamera_Room_int -= RoomCamera_MoveCamera_Room_int;
        On.RoomCamera.WarpMoveCameraActual -= RoomCamera_WarpMoveCameraActual;

        On.RoomCamera.ApplyPositionChange -= RoomCamera_ApplyPositionChange;
    }


    public const string PARALLAXCONTAINER = "HUD";

    public static RenderTexture ScreenLevelTex;

    //Sets/calculates the shader constants
    private void RoomCamera_ctor(On.RoomCamera.orig_ctor orig, RoomCamera self, RainWorldGame game, int cameraNumber)
    {
        orig(self, game, cameraNumber);

        try
        {
            //create RandomWrite texture
            SetupScreenLevelTex();

            //setup CameraData
            CameraData data = self.InstantiateData();
            data.camPos.Set(-1, -1); //don't lerp from previous camera's position

            //determine keywords for FShader
            List<string> keywords = new();
            if (Options.LimitProjection) keywords.Add("LZC_LIMITPROJECTION");
            if (Options.DynamicOptimization) keywords.Add("LZC_DYNAMICOPTIMIZATION");
            if (Options.TwoLayers) keywords.Add("LZC_PROCESSLAYER2");
            if (Options.BackgroundNoise > 0.001f) keywords.Add("LZC_BACKGROUNDNOISE");
            switch (Options.DepthCurve)
            {
                case Options.DepthCurveOptions.EXTREME: keywords.Add("LZC_DEPTHCURVE"); break;
                case Options.DepthCurveOptions.PARABOLIC: keywords.Add("LZC_DEPTHCURVE"); keywords.Add("LZC_INVDEPTHCURVE"); break;
                case Options.DepthCurveOptions.INVERSE: keywords.Add("LZC_INVDEPTHCURVE"); break;
            }
            TrueParallaxFShader.keywords = keywords.ToArray();

            //determine keywords for material
            if (Options.SimplerLayers) ThicknessMapMaterial.EnableKeyword("LZC_SIMPLERLAYERS");
            else ThicknessMapMaterial.DisableKeyword("LZC_SIMPLERLAYERS");

            //set constants for material
            ThicknessMapMaterial.SetInt("LZC_BackgroundTestNum", 22);
            ThicknessMapMaterial.SetFloat("LZC_ProjectionMod", Options.ThicknessMod);
            ThicknessMapMaterial.SetFloat("LZC_MinObjectDepth", Options.MinObjectThickness);

            //add full screen effect to camera
            //put it in HUD so that it's after all the bloom effects. It's a bit unfortunate, but too many objects in Bloom layer reference the LevelTex
            data.sprite = new(Futile.whiteElement) { shader = TrueParallaxFShader, width = self.sSize.x, height = self.sSize.y, anchorX = 0, anchorY = 0 };
            self.ReturnFContainer(PARALLAXCONTAINER).AddChild(data.sprite);

            //setup constants
            data.needSetConstants = true; //can't set them here because the material isn't created yet, so we'll have to wait until it is

            Log("Setup shader constants", 2);

        }
        catch (Exception ex) { Error(ex); }
    }

    private static void SetCameraConstants(Material mat)
    {
        mat.SetFloat(ShadWarp, Options.Warp);

        int testNum = Mathf.Max(2, (int)Mathf.Ceil(Mathf.Abs(Options.Warp) * Options.MaxWarp / Options.OptimizationFac));
        mat.SetInt(ShadTestNum, testNum);
        mat.SetFloat(ShadStepSize, 1.0f / testNum);

        mat.SetFloat("LZC_ConvergenceScale", Options.ConvergenceScale);
        mat.SetFloat("LZC_AntiAliasingFac", Options.AntiAliasing);
        mat.SetFloat("LZC_MaxProjection", Options.MaxProjection);

        mat.SetFloat("LZC_PivotDepth", Options.PivotDepth);
        mat.SetFloat("LZC_Layer30Depth", 1.0f / Options.BackgroundDepth);
        mat.SetFloat("LZC_BackgroundNoise", Options.BackgroundNoise);
    }

    private static void SetupScreenLevelTex()
    {
        Vector2 sSize = Custom.rainWorld.screenSize;
        int w = Mathf.RoundToInt(sSize.x), h = Mathf.RoundToInt(sSize.y); //idk when exactly this happens

        RenderTextureFormat format = Options.TwoLayers ? RenderTextureFormat.ARGB32 : RenderTextureFormat.R8;

        if (ScreenLevelTex == null || ScreenLevelTex.width != w || ScreenLevelTex.height != h || ScreenLevelTex.format != format)
        {
            ScreenLevelTex?.Release();
            ScreenLevelTex = new(w, h, 0, format)
            {
                filterMode = 0,
                enableRandomWrite = true
            };
            ScreenLevelTex.Create();

            //Graphics.ClearRandomWriteTargets(); //I don't know if this is necessary or not...
            Graphics.SetRandomWriteTarget(1, ScreenLevelTex);

            Log("Created ScreenLevelTex. System RandomWrite textures: " + SystemInfo.supportedRandomWriteTargetCount, 2);
        }
    }


    //A couple full screen effects (LevelMelt2, Fog) should be applied AFTER the parallax
    //Also disable WetTerrain displacing the pixels and causing visual oddities.
    private void FixFullScreenEffect(RoomCamera self)
    {
        //if (self.fullScreenEffect != null && self.fullScreenEffect.container != self.ReturnFContainer("Bloom"))
        //self.SetUpFullScreenEffect("Bloom");
        Shader.SetGlobalFloat(RainWorld.ShadPropWetTerrain, 0); //disable wet terrain; it only makes things worse, sadly
    }
    private void RoomCamera_WarpMoveCameraActual(On.RoomCamera.orig_WarpMoveCameraActual orig, RoomCamera self, Room newRoom, int camPos)
    {
        orig(self, newRoom, camPos);
        FixFullScreenEffect(self);
    }
    private void RoomCamera_MoveCamera_Room_int(On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self, Room newRoom, int camPos)
    {
        orig(self, newRoom, camPos);
        FixFullScreenEffect(self);
    }

    //Actually adds the shader to the LevelTexCombiner whenever the LevelTexCombiner gets cleared
    //ALSO attempts to resolution scale...
    //And builds the 2nd and 3rd layers...
    private void RoomCamera_ApplyPositionChange(On.RoomCamera.orig_ApplyPositionChange orig, RoomCamera self)
    {
        orig(self);

        try
        {
            if (!self.TryGetData(out CameraData data)) return;

            data.camPos.Set(-1, -1);

            if (Options.TwoLayers)
            {
                Texture lev = LevTex(self);
                if (data.layer2Textures.temp == null || data.layer2Textures.temp.width != lev.width || data.layer2Textures.temp.height != lev.height)
                {
                    data.layer2Textures.temp?.Release();
                    data.layer2Textures.temp = new(lev.width, lev.height, 0, DefaultFormat.LDR) { filterMode = 0 };
                }
                Graphics.Blit(lev, data.layer2Textures.temp, ThicknessMapMaterial);
                Shader.SetGlobalTexture("_LZC_Layer2Tex", data.layer2Textures.temp);
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    //Weird SBCameraScroll practices...
    private static Texture LevTex(RoomCamera self) => SBCameraScrollEnabled ? self.levelGraphic?._atlas?.texture : self.levelTexture;


    //Sets the CamPos
    public void SetCamPos(RoomCamera self, float moveMod = 1)
    {
        try
        {
            if (!self.TryGetData(out CameraData data)) return;

            if (data.needSetConstants && data.SpriteMaterial != null)
            {
                SetCameraConstants(data.SpriteMaterial);
                data.needSetConstants = false;
                Log("Set up camera constants for camera#" + self.cameraNumber, 2);
            }

            Vector2 pos = new(0.5f, 0.5f);

            //Follow creatures
            var crit = self.followAbstractCreature?.realizedCreature;
            if (!Options.AlwaysCentered && crit != null)
            {
                Vector2? critPos = (crit.inShortcut ? self.game.shortcuts.OnScreenPositionOfInShortCutCreature(self.room, crit) : crit.mainBodyChunk.pos);
                if (critPos != null)
                {
                    pos = (critPos.Value - self.pos
                        + (self.followCreatureInputForward + self.leanPos) * 2f) //add some offset for movement
                        / self.sSize;
                }
            }

            //Mouse movement
            if (Options.MouseSensitivity > 0)
            {
                try
                {
                    float mouseX = Options.MouseSensitivity * Input.GetAxis("Mouse X") * 0.25f;
                    if (mouseX != 0f)
                    {
                        float strength = Mathf.Clamp01(Mathf.Abs(mouseX));
                        pos.x += strength * ((mouseX > 0 ? 1f : 0f) - pos.x);
                    }

                    float mouseY = Options.MouseSensitivity * Input.GetAxis("Mouse Y") * 0.25f;// * 0.5625f; //0.5625 = 9/16 
                    if (mouseY != 0f)
                    {
                        float strength = Mathf.Clamp01(Mathf.Abs(mouseY));
                        pos.y += strength * ((mouseY > 0 ? 1f : 0f) - pos.y);
                    }
                }
                catch { }
            }

            pos.x = Mathf.Clamp01(pos.x);
            pos.y = Mathf.Clamp01(pos.y);

            if (Options.InvertPos)
                pos = Vector2.one - pos;

            //Actually change camera position
            if (data.camPos.x < 0 || data.camPos.y < 0) //invalid old camPos; don't lerp with it
            {
                data.camPos = pos;
            }
            else
            {
                data.camPos = new(
                    Custom.LerpAndTick(data.camPos.x, pos.x, moveMod * Options.CameraMoveSpeed, moveMod * 0.001f),
                    Custom.LerpAndTick(data.camPos.y, pos.y, moveMod * Options.CameraMoveSpeed, moveMod * 0.001f)
                    );
            }

            Material mat = data.SpriteMaterial;
            if (mat != null)
            {
                mat.SetVector(ShadCamPos, data.camPos);
                if (Options.DynamicZoom > 0)
                {
                    Vector2 camDiff2 = data.camPos - new Vector2(0.5f, 0.5f);
                    camDiff2 *= camDiff2;

                    float centerDistance = Mathf.Max(camDiff2.x, camDiff2.y);
                    centerDistance = 4 * Mathf.LerpUnclamped(camDiff2.x + camDiff2.y, centerDistance, centerDistance); //if centerDistance is low, make it circular rather than square
                    float centerWarpFac = Mathf.LerpUnclamped(1, centerDistance * (2 - centerDistance), Options.DynamicZoom);

                    float warp = Options.Warp * centerWarpFac;
                    mat.SetFloat(ShadWarp, warp);

                    int testNum = Mathf.Max(2, (int)Mathf.Ceil(Mathf.Abs(warp) * Options.MaxWarp / Options.OptimizationFac));
                    mat.SetInt(ShadTestNum, testNum);
                    mat.SetFloat(ShadStepSize, 1.0f / testNum);
                }
            }
        }
        catch (Exception ex) { Error(ex); }
    }
    //private void RoomCamera_GetCameraBestIndex(On.RoomCamera.orig_GetCameraBestIndex orig, RoomCamera self)
    private void RoomCamera_DrawUpdate(On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
    {
        orig(self, timeStacker, timeSpeed);

        SetCamPos(self, 0.5f * timeSpeed);
    }

    private void RoomCamera_Update(On.RoomCamera.orig_Update orig, RoomCamera self)
    {
        try
        {
            SetupScreenLevelTex(); //just in case something happened to it

            if (self.TryGetData(out CameraData data))
            {
                //add back the parallax sprite if it was removed for some reason
                if (data.sprite == null
                    || data.sprite.container == null
                    || data.sprite.container.GetChildIndex(data.sprite) < 0)
                {
                    FContainer container = self.ReturnFContainer(PARALLAXCONTAINER);
                    if (container == null)
                        Error("Parallax container is null for camera# " + self.cameraNumber);
                    else
                    {
                        container.AddChildAtIndex(data.sprite, 0);
                        Log("Parallax sprite was removed from container! Re-added it to container", 2);
                    }
                }
            }
        }
        catch (Exception ex) { Error(ex); }

        orig(self);

        SetCamPos(self, 0.5f);
    }

    #endregion

}
