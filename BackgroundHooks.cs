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
            if (Options.BackgroundShift != 0)
            {
                //find the closest camera and use it
                var cameras = self.room.game.cameras;
                float lowestCamDist = float.PositiveInfinity;
                RoomCamera lowestCam = null;
                foreach (var cam in cameras)
                {
                    float dist = (cam.pos - camPos).sqrMagnitude;
                    if (cam.room == self.room && dist < lowestCamDist) { lowestCamDist = dist; lowestCam = cam; }
                }
                if (lowestCam != null && lowestCam.TryGetData(out CameraData data))
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

        return orig(self, pos, depth, camPos, hDisplace);
    }

    private Vector2 OuterRimView_DrawPos(On.Watcher.OuterRimView.orig_DrawPos orig, Watcher.OuterRimView self, BackgroundScene.BackgroundSceneElement element, Vector2 camPos, RoomCamera camera)
    {
        try
        {
            if (Options.BackgroundShift != 0 && camera.TryGetData(out CameraData data))
                camPos += data.BackgroundShift;
        }
        catch (Exception ex) { Error(ex); }

        return orig(self, element, camPos, camera);
    }
    #endregion
}
