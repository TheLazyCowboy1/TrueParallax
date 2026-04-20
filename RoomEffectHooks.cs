using System;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks
    //WetTerrain hook + LevelHeat
    private void RoomCamera_MoveCamera_Room_int(On.RoomCamera.orig_MoveCamera_Room_int orig, RoomCamera self, Room newRoom, int camPos)
    {
        orig(self, newRoom, camPos);
        DisableWetTerrain();
        SetupCameraWetTerrain(self);
        SetupCameraLevelHeat(self);
    }
    //WetTerrain hook + LevelHeat
    private void RoomCamera_WarpMoveCameraActual(On.RoomCamera.orig_WarpMoveCameraActual orig, RoomCamera self, Room newRoom, int camPos)
    {
        orig(self, newRoom, camPos);
        DisableWetTerrain();
        SetupCameraWetTerrain(self);
        SetupCameraLevelHeat(self);
    }

    //Move potentially problematic fullScreenEffects to the correct container
    private void RoomCamera_ApplyPalette(On.RoomCamera.orig_ApplyPalette orig, RoomCamera self)
    {
        orig(self);

        try
        {
            if (self.fullScreenEffect == null) return;

            string name = self.fullScreenEffect.shader.name;
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
        }
        catch (Exception ex) { Error(ex); }
    }
    #endregion

    //Disable WetTerrain, which displaces the pixels and causes visual artefacts.
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
                mat.SetFloat("LZC_LevelHeatAmount", camera.levelGraphic.alpha * Options.LevelHeatFac);
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
