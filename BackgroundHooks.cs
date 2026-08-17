using System;
using System.Linq;
using UnityEngine;
using Watcher;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks

    private Vector2 BackgroundScene_DrawPos(On.BackgroundScene.orig_DrawPos orig, BackgroundScene self, Vector2 pos, float depth, Vector2 camPos, float hDisplace)
    {
        FixBackgroundCamPos(self, CurrentlyRenderingCamera, ref camPos);

        return ShiftBackgroundOutput(CurrentlyRenderingCamera, orig(self, pos, depth, camPos, hDisplace));
    }

    private Vector2 OuterRimView_DrawPos(On.Watcher.OuterRimView.orig_DrawPos orig, Watcher.OuterRimView self, BackgroundScene.BackgroundSceneElement element, Vector2 camPos, RoomCamera camera)
    {
        FixBackgroundCamPos(self, camera, ref camPos);

        return ShiftBackgroundOutput(camera, orig(self, element, camPos, camera));
    }
    private Vector2 RotWormScene_DrawPos(On.RotWormScene.orig_DrawPos orig, RotWormScene self, BackgroundScene.BackgroundSceneElement element, Vector2 camPos)
    {
        FixBackgroundCamPos(self, CurrentlyRenderingCamera, ref camPos);

        return ShiftBackgroundOutput(CurrentlyRenderingCamera, orig(self, element, camPos));
    }

    public void BackgroundHooks_RoomCamera_DrawUpdate(Action orig, RoomCamera self, float timeStacker, float timeSpeed)
    {
        try
        {
            BackgroundScene scene = self.room.updateList.FirstOrDefault(uad => uad is BackgroundScene) as BackgroundScene;
            if (Options.RotateBackground == 0 || scene == null || !self.TryGetData(out CameraData data))
            {
                orig();
                return;
            }

            //remember old convergencePoint
            Vector2 convergencePoint = scene.convergencePoint;
            Vector2 perspectiveCenter = new();

            //offset convergencePoint
            Vector2 offset = Options.RotateBackground * data.CalculateWarp(new(0.5f, 0.5f), 30);
            scene.convergencePoint += offset;
            OuterRimView orv = scene as OuterRimView;
            RotWormScene rws = scene as RotWormScene;
            if (orv != null)
            {
                perspectiveCenter = orv.perspectiveCenter;
                orv.perspectiveCenter += offset;
            }
            else if (rws != null)
            {
                perspectiveCenter = rws.perspectiveCenter;
                rws.perspectiveCenter += offset;
            }

            //orig
            orig();

            //reset convergencePoint to its old value
            scene.convergencePoint = convergencePoint;
            if (orv != null)
                orv.perspectiveCenter = perspectiveCenter;
            else if (rws != null)
                rws.perspectiveCenter = perspectiveCenter;
        }
        catch (Exception ex) { Error(ex); orig(); }
    }

    #endregion

    #region Calculations

    private int DontBackgroundFix = 0;

    private void FixBackgroundCamPos(BackgroundScene self, RoomCamera camera, ref Vector2 camPos)
    {
        try
        {
            if (camera == null || !camera.TryGetData(out CameraData data))
                return;

            //smooth background movement
            if (Options.FixBackgroundJitter && Options.EveryOtherPixel)
                camPos += new Vector2(Mathf.Floor(data.CurrentUVOffset.x), Mathf.Floor(data.CurrentUVOffset.y));

            //background shift
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
        catch (Exception ex) { Error(ex); }
    }
    private Vector2 ShiftBackgroundOutput(RoomCamera camera, Vector2 pos)
    {
        try //fix background jitter
        {
            if (DontBackgroundFix > 0)
                DontBackgroundFix--;
            else if (Options.FixBackgroundJitter && camera != null && camera.TryGetData(out CameraData data))
                pos += data.BackgroundFixOffset;
        }
        catch (Exception ex) { Error(ex); }
        return pos;
    }

    #endregion

    #region ManualSpriteFixing

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

    private void Floor_DrawSprites(On.RoofTopView.Floor.orig_DrawSprites orig, RoofTopView.Floor self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        try
        {
            if (Options.FixBackgroundJitter && Options.EveryOtherPixel && rCam.TryGetData(out CameraData data))
                camPos.x += Mathf.Floor(data.CurrentUVOffset.x); //only change x
        } catch (Exception ex) { Error(ex); }

        //default
        orig(self, sLeaser, rCam, timeStacker, camPos);
        
        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, false);
    }
    [Obsolete]
    private void DustWave_DrawSprites(On.RoofTopView.DustWave.orig_DrawSprites orig, RoofTopView.DustWave self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, true);
    }

    private void Rubble_DrawSprites(On.RoofTopView.Rubble.orig_DrawSprites orig, RoofTopView.Rubble self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, false);
    }

    private void Simple2DBackgroundIllustration_DrawSprites(On.BackgroundScene.Simple2DBackgroundIllustration.orig_DrawSprites orig, BackgroundScene.Simple2DBackgroundIllustration self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
    {
        orig(self, sLeaser, rCam, timeStacker, camPos);

        sLeaser.sprites[0].SetPosition(self.pos);
        OffsetBackgroundSprite(rCam, sLeaser.sprites[0], true, true);
    }

    #endregion
}
