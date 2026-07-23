using System;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks
    private int DontBackgroundFix = 0;

    private Vector2 BackgroundScene_DrawPos(On.BackgroundScene.orig_DrawPos orig, BackgroundScene self, Vector2 pos, float depth, Vector2 camPos, float hDisplace)
    {
        try
        {
            if (CurrentlyRenderingCamera != null && CurrentlyRenderingCamera.TryGetData(out CameraData data))
            {
                if (Options.FixBackgroundJitter && Options.EveryOtherPixel)
                    camPos += new Vector2(Mathf.Floor(data.CurrentUVOffset.x), Mathf.Floor(data.CurrentUVOffset.y));

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
            if (DontBackgroundFix > 0)
                DontBackgroundFix--;
            else if (Options.FixBackgroundJitter && CurrentlyRenderingCamera != null && CurrentlyRenderingCamera.TryGetData(out CameraData data2))
                result += data2.BackgroundFixOffset;
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
                    camPos += new Vector2(Mathf.Floor(data.CurrentUVOffset.x), Mathf.Floor(data.CurrentUVOffset.y));

                if (Options.BackgroundShift != 0)
                    camPos += data.BackgroundShift;
            }
        }
        catch (Exception ex) { Error(ex); }

        //return orig(self, element, camPos, camera);
        Vector2 result = orig(self, element, camPos, camera);
        try
        {
            if (DontBackgroundFix > 0)
                DontBackgroundFix--;
            else if (Options.FixBackgroundJitter && camera.TryGetData(out CameraData data2))
                result += data2.BackgroundFixOffset;
        }
        catch (Exception ex) { Error(ex); }

        return result;
    }

    private void OffsetBackgroundSprite(RoomCamera cam, FSprite sprite, bool offsetX, bool offsetY)
    {
        try
        {
            if (Options.FixBackgroundJitter && cam.TryGetData(out CameraData data))
            {
                if (offsetX) sprite.x += data.BackgroundFixOffset.x;
                if (offsetY) sprite.y += data.BackgroundFixOffset.y;
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    private void CloseCloud_DrawSprites(On.AboveCloudsView.CloseCloud.orig_DrawSprites orig, AboveCloudsView.CloseCloud self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        DontBackgroundFix = 1;
        orig(self, sLeaser, rCam, timeStacker, camPos);

        sLeaser.sprites[0].SetPosition(683, 0);
        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, true);
        OffsetBackgroundSprite(rCam, sLeaser.sprites[1], true, true);
    }

    private void DistantCloud_DrawSprites(On.AboveCloudsView.DistantCloud.orig_DrawSprites orig, AboveCloudsView.DistantCloud self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        DontBackgroundFix = 1;
        orig(self, sLeaser, rCam, timeStacker, camPos);

        sLeaser.sprites[0].SetPosition(683, 0);
        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, true);
        OffsetBackgroundSprite(rCam, sLeaser.sprites[1], true, true);
    }

    private void FlyingCloud_DrawSprites(On.AboveCloudsView.FlyingCloud.orig_DrawSprites orig, AboveCloudsView.FlyingCloud self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        DontBackgroundFix = 1;
        orig(self, sLeaser, rCam, timeStacker, camPos);

        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, true);
    }

    private void Simple2DBackgroundIllustration_DrawSprites(On.BackgroundScene.Simple2DBackgroundIllustration.orig_DrawSprites orig, BackgroundScene.Simple2DBackgroundIllustration self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        sLeaser.sprites[0].SetPosition(self.pos);
        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, true);
    }

    #endregion
}
