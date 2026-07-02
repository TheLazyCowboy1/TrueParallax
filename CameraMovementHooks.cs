using System;
using System.Linq;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{
    #region Hooks
    private void RoomCamera_DrawUpdate(On.RoomCamera.orig_DrawUpdate orig, RoomCamera self, float timeStacker, float timeSpeed)
    {
        try
        {
            SetupScreenLevelTex(); //just in case something happened to it

            if (self.TryGetData(out CameraData data))
            {
                bool inactive = data.Inactive;
                //add back the parallax sprite if it was removed for some reason
                if (inactive && !ParallaxShouldBeInactive(data))
                {
                    FContainer container = self.ReturnFContainer(PARALLAXCONTAINER);
                    if (container == null)
                        Error("Parallax container is null for camera# " + self.cameraNumber);
                    else
                    {
                        container.AddChildAtIndex(data.sprite, 0);
                        Log("Re-added parallax sprite to container.", 2);
                    }
                }
                //remove the parallax sprite if it shouldn't be there; basically just for ending cutscenes
                else if (data.sprite != null && !inactive && ParallaxShouldBeInactive(data))
                {
                    data.sprite.RemoveFromContainer();
                    Log("Removed parallax sprite from container.", 2);
                }

                if (Options.DynamicAdjustmentThreshold > 0)
                    data.UpdateDeltaTime();
            }

        }
        catch (Exception ex) { Error(ex); }

        orig(self, timeStacker, timeSpeed);

        SetCamPos(self, timeStacker);
    }

    private void RoomCamera_Update(On.RoomCamera.orig_Update orig, RoomCamera self)
    {
        orig(self);

        UpdateCamPos(self);
    }
    #endregion

    #region Calculations

    public static bool ParallaxShouldBeInactive(CameraData data) => data.camera.voidSeaMode || data.camera.freeMoveRect != null || (!Options.SplitscreenParallax && data.camera.cameraNumber > 0);

    public static Vector2? GetCritPos(RoomCamera self, CameraData data, bool updateOffset = false, float moveSpeed = 0)
    {
        Vector2? critPos = null;

        //Follow creatures
        var crit = self.followAbstractCreature?.realizedCreature;
        if (crit != null)
        {
            critPos = (crit.inShortcut ? self.game.shortcuts.OnScreenPositionOfInShortCutCreature(self.room, crit) : crit.mainBodyChunk.pos);
        }

        //Update offset
        if (updateOffset)
        {
            bool readInput = false;
            if (critPos != null)
            {
                //inch offset toward 0
                data.critFollowOffset = LerpAndTick(data.critFollowOffset, Vector2.zero, moveSpeed, moveSpeed * 0.01f);

                //offset by player input
                var input = (crit as Player)?.input[0] ?? crit.inputWithDiagonals;
                if (input != null)
                {
                    data.critFollowOffset = Vector2.ClampMagnitude(data.critFollowOffset + input.Value.analogueDir * Options.CameraInputOffset * moveSpeed, Options.CameraInputOffset);
                    readInput = true;
                }
            }
            if (!readInput) data.critFollowOffset.Set(0, 0);
        }

        return critPos;
    }
    //Determines what the CamPos should be
    public static void UpdateCamPos(RoomCamera self, float moveMod = 1)
    {
        try
        {
            if (!self.TryGetData(out CameraData data)) return;


            float moveSpeed = Options.CameraMoveSpeed * moveMod;

            Vector2 pos = new(0.5f, 0.5f);

            bool sbCameraMode = Options.UseSBPlayerPos && Plugin.SBCameraScrollEnabled;
            if (sbCameraMode)
            {
                pos = ModCompat.SBCameraScrollMod.GetSBPlayerPos(self);
            }
            else if (!Options.AlwaysCentered) //calculate player on-screen position
            {
                Vector2? critPos = GetCritPos(self, data, true);

                if (Options.CameraBasedPosition)
                {
                    pos = (self.pos + data.critFollowOffset + 0.5f * self.sSize) / new Vector2(self.room.PixelWidth, self.room.PixelHeight);
                }
                else if (critPos != null)
                {
                    pos = (critPos.Value - self.pos + data.critFollowOffset) / self.sSize;
                }
            }

            //multiply by scale to accomodate SBCameraScroll's zoom feature
            Vector2 half = new(0.5f, 0.5f);
            pos = (pos - half) * self.SpriteLayers[0].scale + half;

            //Mouse movement
            if (Options.MouseSensitivity > 0)
            {
                try
                {
                    //inch offset toward 0
                    data.mouseOffset = LerpAndTick(data.mouseOffset, Vector2.zero, moveSpeed * 1.5f, moveSpeed * 0.01f);

                    float mouseX = Options.MouseSensitivity * Input.GetAxis("Mouse X");
                    if (mouseX != 0f)
                    {
                        data.mouseOffset.x += Mathf.Clamp(mouseX, -10, 10) * Options.CameraMoveSpeed * moveSpeed * 1.5f; //clamp just for sanity
                    }

                    float mouseY = Options.MouseSensitivity * Input.GetAxis("Mouse Y");// * 0.5625f; //0.5625 = 9/16 
                    if (mouseY != 0f)
                    {
                        data.mouseOffset.y += Mathf.Clamp(mouseY, -10, 10) * Options.CameraMoveSpeed * moveSpeed * 1.5f;
                    }

                    pos += data.mouseOffset;
                }
                catch { }
            }

            pos.Set(Mathf.Clamp01(pos.x), Mathf.Clamp01(pos.y)); //clamp 0 to 1

            if (Options.CameraMotionCurve != 0) //apply smoothing curve
                pos.Set(SmoothCurve(pos.x, Options.CameraMotionCurve), SmoothCurve(pos.y, Options.CameraMotionCurve));

            if (Options.InvertPos) //invert position
                pos = Vector2.one - pos;


            //Actually change camera position

            if (data.CamPos.x < 0 || data.CamPos.y < 0) //invalid old camPos; don't lerp with it
            {
                data.CamPos = pos;
                data.lastCamPos = pos; //also set lastCamPos, so there's no interpolation
                return;
            }

            if (sbCameraMode)
            {
                if (ModCompat.SBCameraScrollMod.UpdateSBCameraPos(self, out Vector2 newPos, data.CamPos))
                {
                    data.CamPos = newPos;
                    return;
                }
            }

            if ((data.CamPos - pos).sqrMagnitude > Options.CameraStopDistance * Options.CameraStopDistance) //don't move when very close
            {
                data.CamPos = LerpAndTick(data.CamPos, pos, moveSpeed, moveSpeed * 0.005f);
            }
        }
        catch (Exception ex) { Error(ex); }
    }
    private static Vector2 LerpAndTick(Vector2 a, Vector2 b, float lerp, float tick)
    {
        //lerp
        Vector2 l = Vector2.LerpUnclamped(a, b, lerp);
        if (l == b) return l; //already there

        //tick
        Vector2 d = b - l; //desired direction
        Vector2 t = d * Mathf.Min(tick / d.magnitude, 1); //don't move more than magnitude = don't overshoot
        return l + t;
    }
    private static float SmoothCurve(float x, float s) => x*(3 - 3*s + s*x*(6 - 4*x)) / (3 - s);

    //Sets the CamPos
    public static void SetCamPos(RoomCamera self, float lerpFac)
    {
        try
        {
            if (!self.TryGetData(out CameraData data)) return;

            if (data.needSetConstants && data.SpriteMaterial != null)
            {
                SetCameraConstants(data);
                data.needSetConstants = false;
                Log("Set up camera constants for camera#" + self.cameraNumber, 2);
            }

            Material mat = data.SpriteMaterial;

            //Background Scene stuff
            if (Options.BackDepthForScenesOnly)
            {
                bool old = data.activeBackgroundScene;
                data.activeBackgroundScene = self.room != null && self.room.updateList.Any(uad => uad is BackgroundScene);
                if (mat != null && old != data.activeBackgroundScene) //if it has changed
                {
                    mat.SetFloat("LZC_Layer30Depth", data.activeBackgroundScene ? 1.0f / Options.BackgroundDepth : 1);
                    mat.SetFloat("LZC_MaxProjection", data.CurrentMaxProjection);
                    Log("Updated activeBackgroundScene for camera#" + self.cameraNumber, 2);
                }
            }

            //set the CamPos in the actual material
            if (mat != null)
            {
                mat.SetVector(ShadPropCamPos, data.CamPos);

                if (Options.IsActiveDynamicZoom)
                {
                    Vector2 camDiff2 = data.CamPos - new Vector2(0.5f, 0.5f);
                    camDiff2 *= camDiff2;

                    float centerDistance = Mathf.Max(camDiff2.x, camDiff2.y);
                    centerDistance = 4 * Mathf.LerpUnclamped(camDiff2.x + camDiff2.y, centerDistance, centerDistance); //if centerDistance is low, make it circular rather than square
                    float centerWarpFac = Mathf.LerpUnclamped(1, centerDistance * (2 - centerDistance), Options.DynamicZoom);

                    data.currentWarp = data.totalWarp * centerWarpFac;

                    SetWarpConstants(data);
                }
                else if (Options.IsActiveCenterOptimization) //test a significant optimization (up to half) without using DynamicOptimization
                {
                    SetWarpConstants(data);
                }
            }
        }
        catch (Exception ex) { Error(ex); }
    }
    #endregion

}
