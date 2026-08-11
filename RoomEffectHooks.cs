using System;
using Unity.Mathematics;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks
    //WetTerrain hook + LevelHeat
    private void RoomCamera_MoveCamera_Room_int(On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self, Room newRoom, int camPos)
    {
        bool wetTerrain = newRoom.roomSettings.wetTerrain;
        newRoom.roomSettings.wetTerrain = false; //this method works with SplitScreen Co-op
        orig(self, newRoom, camPos);
        newRoom.roomSettings.wetTerrain = wetTerrain;

        //DisableWetTerrain();
        SetupCameraWetTerrain(self);
        SetupCameraLevelHeat(self);
    }
    //WetTerrain hook + LevelHeat
    private void RoomCamera_WarpMoveCameraActual(On.RoomCamera.orig_WarpMoveCameraActual orig, RoomCamera self, Room newRoom, int camPos)
    {
        bool wetTerrain = newRoom.roomSettings.wetTerrain;
        newRoom.roomSettings.wetTerrain = false;
        orig(self, newRoom, camPos);
        newRoom.roomSettings.wetTerrain = wetTerrain;

        //DisableWetTerrain();
        SetupCameraWetTerrain(self);
        SetupCameraLevelHeat(self);
    }

    //Move potentially problematic fullScreenEffects to the correct container
    private void RoomCamera_ApplyPalette(On.RoomCamera.orig_ApplyPalette orig, RoomCamera self)
    {
        ManageSecondFullScreenEffect(self);

        orig(self);

        try
        {
            if (self.fullScreenEffect == null) return;

            string name = self.fullScreenEffect.shader.name;

            //SBCameraScroll (perhaps accidentally) changes FShader names, e.g: Fog => SBCameraScroll/Fog
            int index = name.IndexOf('/');
            if (index >= 0)
                name = name.Substring(index+1); //cut out everything before the /

            bool reads = ShaderReadsLevel(name);
            bool warps = ShaderWarpsLevel(name);
            if (reads && warps)
            {
                self.fullScreenEffect.RemoveFromContainer();
                self.fullScreenEffect = null;
                Log("Removed problematic fullScreenEffect: " + name, 2);
            }
            else if (!reads) //move most fullScreenEffects to parallax container by default
            {
                self.fullScreenEffect.RemoveFromContainer();
                self.ReturnFContainer(PARALLAXCONTAINER).AddChild(self.fullScreenEffect);
                Log("Moved fullScreenEffect to parallax container: " + name, 2);
            }
            else
            {
                Log("Kept fullScreenEffect in its original container: " + name, 2);
            }
        }
        catch (Exception ex) { Error(ex); }
    }


    //Stop distortion from messing with camera please
    private void CellDistortion_InitiateSprites(On.MoreSlugcats.CellDistortion.orig_InitiateSprites orig, MoreSlugcats.CellDistortion self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        //move out of Bloom container and into parallax container
        try
        {
            sLeaser.sprites[0].RemoveFromContainer();
            rCam.ReturnFContainer(PARALLAXCONTAINER).AddChild(sLeaser.sprites[0]);
        }
        catch (Exception ex) { Error(ex); }
    }

    //Optionally disables decals flickering
    private int CustomDecal_GetIdealGridDiv(On.CustomDecal.orig_GetIdealGridDiv orig, CustomDecal self)
    {
        try
        {
            if (Options.FixDecalFlickering)
            {
                for (int i = 0; i < self.quad.Length; i++)
                    self.quad[i] *= 2; //scale up so that we get a bigger gridDiv

                int val = orig(self);

                for (int i = 0; i < self.quad.Length; i++)
                    self.quad[i] *= 0.5f; //scale back down

                return val;
            }
        }
        catch (Exception ex) { Error(ex); }

        return orig(self);
    }
    private void CustomDecal_UpdateVerts(On.CustomDecal.orig_UpdateVerts orig, CustomDecal self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        try
        {
            if (!Options.FixDecalFlickering)
                return;
            if (sLeaser.sprites[0] is not TriangleMesh mesh)
                return;
            for (int i = 0; i < mesh.verticeColors.Length; i++)
            {
                Color c = mesh.verticeColors[i];

                //offset vertices
                self.verts[i].y -= 40.0f * c.b * (noise.snoise(new float2(self.verts[i].x, self.verts[i].y * 0.1f) * 0.015f) + 0.75f);

                c.b = 0; //disable blue channel == disable erosion
                mesh.verticeColors[i] = c;
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    #endregion

    //Disable WetTerrain, which displaces the pixels and causes visual artefacts.
    //APPARENTLY, this doesn't work well with SplitScreen Co-op, so instead I'll temporarily disable the room effect entirely
    private static void DisableWetTerrain() => Shader.SetGlobalFloat(RainWorld.ShadPropWetTerrain, 0);

    #region FullScreenEffectFilter
    private static bool ShaderWarpsLevel(string FShaderName) => FShaderName switch
    {
        "LevelMelt2" => true,
        "SkyBloom" => false,
        "LightAndSkyBloom" => false,
        "LightBloom" => false,
        "Fog" => false,
        "Bloom" => false,
        _ => false
    };
    private static bool ShaderReadsLevel(string FShaderName) => FShaderName switch
    {
        "LevelMelt2" => false, //surprising!
        "SkyBloom" => false,
        "LightAndSkyBloom" => true,
        "LightBloom" => true,
        "Fog" => true,
        "Bloom" => false,
        _ => false
    };
    #endregion

    #region CameraModifications
    private static void ManageSecondFullScreenEffect(RoomCamera self)
    {
        try //stupid LightAndSkyBloom stuff
        {
            if (self.TryGetData(out CameraData data))
            {
                bool useSecondEffect = false;
                if (Options.ImproveSkyAndLightBloom)
                {
                    if (data.alteredLightAndSkyBloom != null) //fix any previous monkeying
                    {
                        data.alteredLightAndSkyBloom.type = RoomSettings.RoomEffect.Type.SkyAndLightBloom;
                    }

                    RoomSettings settings = self.room.roomSettings;
                    var LSBloom = settings.GetEffect(RoomSettings.RoomEffect.Type.SkyAndLightBloom);
                    if (LSBloom != null)
                    {
                        LSBloom.type = RoomSettings.RoomEffect.Type.LightBurn; //remove the "Sky" part of SkyAndLightBloom
                        data.alteredLightAndSkyBloom = LSBloom;

                        useSecondEffect = true;
                        if (data.secondFullScreenEffect == null)
                        {
                            data.secondFullScreenEffect = new FSprite("Futile_White", true)
                            {
                                scaleX = self.sSize.x / 16f,
                                scaleY = 48f,
                                anchorX = 0f,
                                anchorY = 0f
                            };
                            self.ReturnFContainer(PARALLAXCONTAINER).AddChild(data.secondFullScreenEffect);
                            Log("Set up secondFullScreenEffect in room " + settings.name, 2);
                        }
                        data.secondFullScreenEffect.shader = CustomSkyBloomFShader;
                        data.secondFullScreenEffect.alpha = 1;
                    }
                }

                if (!useSecondEffect)
                {
                    data.secondFullScreenEffect?.RemoveFromContainer();
                    data.secondFullScreenEffect = null;
                }
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    public static void SetupCameraWetTerrain(RoomCamera camera)
    {
        try
        {
            if (!Options.WetTerrain)
                return; //no wet terrain at all
            if (!camera.TryGetData(out CameraData data))
                return; //no camera data somehow
            Material mat = data.SpriteMaterial;
            if (mat == null)
                return; //no material

            if (camera.room.roomSettings.wetTerrain)
                mat.EnableKeyword("LZC_WETTERRAIN");
            else
                mat.DisableKeyword("LZC_WETTERRAIN");
        }
        catch (Exception ex) { Error(ex); }
    }

    public static void SetupCameraLevelHeat(RoomCamera camera)
    {
        try
        {
            if (!Options.LevelHeat)
                return; //no level heat at all
            if (!camera.TryGetData(out CameraData data))
                return; //no camera data somehow
            Material mat = data.SpriteMaterial;
            if (mat == null)
                return; //no material

            string shaderName = camera.levelGraphic.shader.name;
            bool levelHeat = shaderName == "LevelHeat" || shaderName == "LevelMelt";
            if (levelHeat)
            {
                if (shaderName == "LevelHeat")
                {
                    mat.EnableKeyword("levelheat");
                    mat.DisableKeyword("levelmelt");
                }
                else
                {
                    mat.DisableKeyword("levelheat");
                    mat.EnableKeyword("levelmelt");
                }
                mat.SetFloat(ShadPropLevelHeatAmount, camera.levelGraphic.alpha * Options.LevelHeatFac);
            }
            else
            {
                mat.DisableKeyword("levelheat");
                mat.DisableKeyword("levelmelt");
            }
        }
        catch (Exception ex) { Error(ex); }
    }
    #endregion

}
