using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks
    private void RoomCamera_ctor(On.RoomCamera.orig_ctor orig, RoomCamera self, RainWorldGame game, int cameraNumber)
    {
        orig(self, game, cameraNumber);

        try
        {
            //create RandomWrite texture
            SetupScreenLevelTex();

            //set keywords (must be done before sprite is initialized)
            SetKeywords();

            //setup CameraData
            CameraData data = self.CreateData();
            data.CamPos = new(-1, -1); //don't lerp from previous camera's position
            data.needSetConstants = true; //can't set them here because the material isn't created yet, so we'll have to wait until it is

            //add full screen effect to camera

            //resize FContainer array
            int hudIdx = self.SpriteLayerIndex["HUD"];
            Array.Resize(ref self.SpriteLayers, self.SpriteLayers.Length + 1);

            foreach (string key in self.SpriteLayerIndex.Keys.ToArray()) //increase HUD layer indices
            {
                if (self.SpriteLayerIndex[key] >= hudIdx)
                    self.SpriteLayerIndex[key]++;
            }
            for (int i = self.SpriteLayers.Length - 1; i > hudIdx; i--) //shift HUD layers right by one
                self.SpriteLayers[i] = self.SpriteLayers[i - 1];

            //create new container
            self.SpriteLayers[hudIdx] = new();
            self.SpriteLayerIndex.Add(PARALLAXCONTAINER, hudIdx);
            //add the container at the HUD container's current index
            Futile.stage.AddChildAtIndex(self.SpriteLayers[hudIdx], Futile.stage.GetChildIndex(self.SpriteLayers[hudIdx + 1]));

            //create the actual sprite
            data.sprite = new(Futile.whiteElement) { shader = TrueParallaxFShader, width = self.sSize.x, height = self.sSize.y, anchorX = 0, anchorY = 0, x = self.sSize.x * (1-Options.SpriteWidth) };
            self.ReturnFContainer(PARALLAXCONTAINER).AddChild(data.sprite);

            Log("Setup shader constants", 2);

        }
        catch (Exception ex) { Error(ex); }
    }
    #endregion

    #region Other
    //Sets keywords for the shaders, and sets constants for ThicknessMap.shader
    private static void SetKeywords()
    {
        //determine keywords for FShader
        List<string> keywords = new();

        if (Options.LimitProjection) keywords.Add("LZC_LIMITPROJECTION");
        if (Options.DynamicOptimization) keywords.Add("LZC_DYNAMICOPTIMIZATION");
        if (Options.TwoLayers)
        {
            keywords.Add("LZC_PROCESSLAYER2");
            if (Options.BuildCreatureBackground) keywords.Add("LZC_BUILDCREATUREBACKGROUND");
        }
        if (Options.BackgroundNoise > 0.001f) keywords.Add("LZC_BACKGROUNDNOISE");
        keywords.Add(Options.DepthCurve switch
        {
            Options.DepthCurveOptions.INVERSE => "LZC_INVERSEDEPTH",
            Options.DepthCurveOptions.PARABOLIC => "LZC_PARABOLICDEPTH",
            Options.DepthCurveOptions.CUBIC => "LZC_EXTREMEDEPTH",
            Options.DepthCurveOptions.REALAPPROX => "LZC_APPROXREALDEPTH",
            Options.DepthCurveOptions.REALISTIC => "LZC_REALISTICDEPTH",
            _ => "LZC_LINEARDEPTH"
        });
        if (Options.IsActiveSuperAccurateThickness) keywords.Add("LZC_SUPERACCURATETHICKNESS");

        TrueParallaxFShader.keywords = keywords.ToArray();

        //determine keywords for material
        if (Options.SimplerLayers) ThicknessMapMaterial.EnableKeyword("LZC_SIMPLERLAYERS");
        else ThicknessMapMaterial.DisableKeyword("LZC_SIMPLERLAYERS");

        //set constants for material
        ThicknessMapMaterial.SetInt("LZC_BackgroundTestNum", Options.BackgroundTestNum);
        ThicknessMapMaterial.SetFloat("LZC_ProjectionMod", Options.ThicknessMod);
        ThicknessMapMaterial.SetFloat("LZC_MinObjectDepth", Options.MinObjectThickness);
        ThicknessMapMaterial.SetFloat("LZC_MaxDepDiff", Options.MaxDepthDifference);

        Log("Set shader keywords: " + string.Join(", ", TrueParallaxFShader.keywords), 2);
    }

    //Sets constants for TrueParallax.shader
    public static void SetWarpConstants(CameraData data)
    {
        Material mat = data.SpriteMaterial;

        mat.SetFloat(ShadPropWarp, data.currentWarp);

        float maxUsedWarp = Options.IsActiveCenterOptimization ? data.CalcMaxUsedWarp() : data.currentWarp;
        int testNum = Mathf.Max(2, (int)Mathf.Ceil(Mathf.Abs(maxUsedWarp) / Options.OptimizationFac));

        mat.SetInt(ShadPropTestNum, testNum);
        mat.SetFloat(ShadPropStepSize, 1.0f / testNum);

        Vector2 sSize = Custom.rainWorld.screenSize;
        mat.SetVector(ShadPropMoveStepScale, data.currentWarp / testNum * new Vector2(1, sSize.y / sSize.x));
    }
    private static void SetCameraConstants(CameraData data)
    {
        SetWarpConstants(data);

        Material mat = data.SpriteMaterial;

        Vector2 sSize = Custom.rainWorld.screenSize;
        mat.SetVector("LZC_MaxWarp", Options.MaxWarp * new Vector2(1, sSize.x / sSize.y)); //increase MaxWarp.y due to aspect ratio
        mat.SetFloat("LZC_ConvergenceScale", Options.ConvergenceScale);
        mat.SetVector("LZC_GeneralScale", new(1.0f / Options.GeneralScale, 1.0f / Options.GeneralScale));
        mat.SetFloat("LZC_PivotDepth", Options.PivotDepth);
        mat.SetFloat("LZC_Layer30Depth", data.CurrentLayer30Depth);
        mat.SetFloat("LZC_AntiAliasingFac", Options.AntiAliasing);
        mat.SetFloat("LZC_BackgroundNoise", Options.BackgroundNoise);
        mat.SetFloat("LZC_MaxProjection", data.CurrentMaxProjection);

        mat.SetInt("LZC_CreatureBackgroundTests", Options.CreatureBackgroundTests);
        mat.SetInt("LZC_DefaultLevelThickness", Options.DefaultLevelThickness);
        mat.SetFloat("LZC_ProjectionMod", Options.ThicknessMod);
        mat.SetFloat("LZC_MinObjectDepth", Options.MinObjectThickness);
        mat.SetFloat("LZC_MaxDepDiff", Options.MaxDepthDifference);

        if (Options.TwoLayers && data.layer2Textures.First != null) //also set here in case the material wasn't set up yet when generated
            mat.SetTexture(ShadPropLayer2Tex, data.layer2Textures.First);

        SetupCameraLevelHeat(data.camera);
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
    #endregion

}
