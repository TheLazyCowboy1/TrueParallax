using System;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks
    private Vector2 BackgroundScene_DrawPos(On.BackgroundScene.orig_DrawPos orig, BackgroundScene self, Vector2 pos, float depth, Vector2 camPos, float hDisplace)
    {
        try
        {
            if (CurrentlyRenderingCamera != null && CurrentlyRenderingCamera.TryGetData(out CameraData data))
            {
                if (Options.FixBackgroundJitter && Options.EveryOtherPixel)
                {
                    camPos += new Vector2(Mathf.Floor(data.CurrentUVOffset.x), Mathf.Floor(data.CurrentUVOffset.y));
                }

                if (Options.BackgroundShift != 0)
                {
                    if (self is RoofTopView)
                        camPos.x += data.BackgroundShift.x; //only shift x; otherwise it looks really bad
                    //else if (self is AboveCloudsView)
                    //    camPos.y += data.BackgroundShift.y; //only shift y; because the clouds can't be shifted horizontally
                    else
                        camPos += data.BackgroundShift;
                }
            }
        }
        catch (Exception ex) { Error(ex); }

        Vector2 result = orig(self, pos, depth, camPos, hDisplace);
        try
        {
            if (Options.FixBackgroundJitter && CurrentlyRenderingCamera != null && CurrentlyRenderingCamera.TryGetData(out CameraData data2))
                result += data2.CurrentUVOffset;
        }
        catch (Exception ex) { Error(ex); }

        return result;
    }

    private Vector2 OuterRimView_DrawPos(On.Watcher.OuterRimView.orig_DrawPos orig, Watcher.OuterRimView self, BackgroundScene.BackgroundSceneElement element, Vector2 camPos, RoomCamera camera)
    {
        try
        {
            if (camera.TryGetData(out CameraData data))
            {
                if (Options.FixBackgroundJitter && Options.EveryOtherPixel)
                {
                    camPos += new Vector2(Mathf.Floor(data.CurrentUVOffset.x), Mathf.Floor(data.CurrentUVOffset.y));
                }

                if (Options.BackgroundShift != 0)
                    camPos += data.BackgroundShift;
            }
        }
        catch (Exception ex) { Error(ex); }

        //return orig(self, element, camPos, camera);
        Vector2 result = orig(self, element, camPos, camera);
        try
        {
            if (Options.FixBackgroundJitter && camera.TryGetData(out CameraData data2))
                result += data2.CurrentUVOffset;
        }
        catch (Exception ex) { Error(ex); }

        return result;
    }
    #endregion
}
