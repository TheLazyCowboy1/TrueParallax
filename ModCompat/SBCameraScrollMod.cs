using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SBCameraScroll;
using System;
using System.Security;
using System.Security.Permissions;
using TrueParallax.Tools;
using UnityEngine;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TrueParallax.ModCompat;

public static class SBCameraScrollMod
{
    #region Hooks
    private static Hook PositionHook = null;
    private static Hook PositionCamUpdateHook = null;
    private static ILHook CheckBordersHook = null;

    public static void ApplyHooks()
    {
        PositionHook = new((Delegate)RoomCameraMod.UpdateOnScreenPosition, Hook_UpdateOnScreenPosition);
        PositionCamUpdateHook = new(typeof(PositionTypeCamera).GetMethod(nameof(PositionTypeCamera.Update)), Hook_PositionCameraUpdate);
        CheckBordersHook = new(typeof(RoomCameraMod).GetMethod(nameof(RoomCameraMod.CheckBorders)), IL_CheckBorders);
    }
    public static void RemoveHooks()
    {
        PositionHook?.Undo();
        PositionCamUpdateHook?.Undo();
        CheckBordersHook?.Undo();
    }

    private static string LastRoomName;
    private static Vector2 RoomCenter = new(), AreaScale = new();

    private static void Hook_UpdateOnScreenPosition(Action<RoomCamera> orig, RoomCamera room_camera)
    {
        orig(room_camera);
        if (Options.InflateSBCameraFac == 0)
            return;
        if (!room_camera.TryGetData(out CameraData data) || data.Inactive)
            return; //parallax isn't active

        //Vector2 offset = Options.AdjustSBCameraFac * data.CalculateWarp(new(0.5f, 0.5f));
        //room_camera.GetFields().on_screen_position += offset;
        CalcRoomScaleFac(room_camera, data);

        //scale around RoomCenter by AreaScale
        var cameraFields = room_camera.GetFields();
        Vector2 center = RoomCenter - 0.5f * room_camera.sSize; //why does SB offset by -0.5*sSize? Idk. But reflect it here.
        cameraFields.on_screen_position = (cameraFields.on_screen_position - center) * AreaScale + center;
    }
    private static void CalcRoomScaleFac(RoomCamera cam, CameraData data)
    {
        string roomName = cam.room.abstractRoom.name;
        if (roomName != LastRoomName) //only calculate this if it has changed
        {
            //calculate camera movement box
            var roomFields = cam.room.abstractRoom.GetFields();

            Vector2 roomSize = new(roomFields.total_width, roomFields.total_height);
            RoomCenter = roomFields.min_camera_position + 0.5f * roomSize;
            Vector2 movementArea = roomSize - cam.sSize;

            float expand = Mathf.Max(0, Options.InflateSBCameraFac * 2 * data.totalWarp * data.DepthCurve(5f / 30f));
            Vector2 newMovementArea = movementArea + new Vector2(expand, expand * cam.sSize.y / cam.sSize.x);
            AreaScale = movementArea / newMovementArea;
            if (movementArea.x <= 0) AreaScale.x = 1; //don't scale x if the camera can't move horizontally anyway
            if (movementArea.y <= 0) AreaScale.y = 1;
        }
    }

    private class CustomCameraData
    {
        public Vector2 lastPos = new();
        public Vector2 lastDelta = new();
        //public Vector2 lastTargetPos = new();
    }
    private static ResizableArray<CustomCameraData> customDataList = new(2);

    private static void Hook_PositionCameraUpdate(Action<PositionTypeCamera> orig, PositionTypeCamera self)
    {
        try
        {
            if (Options.OverrideSBCamera != Options.SBCameraType.Custom || !self._room_camera.TryGetData(out CameraData data))
            {
                orig(self);
                return;
            }

            RoomCamera cam = self._room_camera;
            Vector2 onScreenPosition = cam.GetFields().on_screen_position;
            RoomCameraMod.UpdateOnScreenPosition(cam); //done just in case

            if (!customDataList.TryGetValue(cam.cameraNumber, out CustomCameraData customData))
                customDataList.Add(cam.cameraNumber, customData = new());

            float moveSpeed = Options.CameraMoveSpeed;
            Vector2? critPos = Plugin.GetCritPos(cam, data, Options.CurrentScreenCamera != Options.ScreenCameraType.Default, moveSpeed);
            if (critPos == null)
            {
                cam.pos = cam.lastPos; //don't move
                return;
            }
            Vector2 origTargetPos = critPos.Value + data.critFollowOffset; //before borders and scaling and curves and whatnot

            //apply borders
            var fields = cam.room.abstractRoom.GetFields();
            Vector2 roomSize = new(fields.total_width, fields.total_height - YBorderSize());
            Vector2 corner = fields.min_camera_position;
            Vector2 border = new(Options.CustomCameraXBorder, Options.CustomCameraYBorder);

            Vector2 playerArea = roomSize - border - border;

            float halfInvZoom = 0.5f * (1.0f / cam.SpriteLayers[0].scale - 1); //crazy SBCameraScroll zoom calculation
            Vector2 sSizeIncrease = cam.sSize * halfInvZoom;
            Vector2 camArea = roomSize - cam.sSize - sSizeIncrease * 2;

            Vector2 fracPos = (origTargetPos - corner - border) / playerArea;

            //apply SmoothCurve
            Vector2 derivSmoothCurve = new(1, 1);
            if (Options.CustomCameraCurve != 0)
            {
                Vector2 curves = Options.CustomCameraCurve * new Vector2(
                    Mathf.Min(1, ProperSmoothFac(camArea.x, playerArea.x)), //don't exceed a curve of 1
                    Mathf.Min(1, ProperSmoothFac(camArea.y, playerArea.y)));
                derivSmoothCurve.x = DerivSmoothCurve2(Mathf.Clamp01(fracPos.x), curves.x);
                fracPos.x = SmoothCurve2(Mathf.Clamp01(fracPos.x), curves.x);
                derivSmoothCurve.y = DerivSmoothCurve2(Mathf.Clamp01(fracPos.y), curves.y);
                fracPos.y = SmoothCurve2(Mathf.Clamp01(fracPos.y), curves.y);
                derivSmoothCurve = (derivSmoothCurve + new Vector2(0.25f, 0.25f)) / 1.25f; //to lessen the severity
            }

            Vector2 targetPos = fracPos * camArea + corner + sSizeIncrease;

            RoomCameraMod.CheckBorders(cam, ref targetPos); //very important step I forgot, lol


            //Actually set position
            if (Options.TransitionsResetCamera && cam.lastPos == onScreenPosition && cam.lastPos != customData.lastPos) //camera position was probably just reset
            {
                cam.pos = targetPos; //no smoothing
                customData.lastDelta.Set(0, 0);
            }
            else
            {
                Vector2 delta = Vector2.zero;
                Vector2 scaledSSize = cam.sSize / cam.SpriteLayers[0].scale;
                Vector2 moveScaleSize = scaledSSize * camArea / playerArea * derivSmoothCurve;

                data.xMovement = Mathf.Abs(targetPos.x - cam.lastPos.x) / moveScaleSize.x > (data.xMovement ? Options.CameraStopDistance : Options.CameraStartDistance);
                //data.xMovement = Mathf.Abs(origTargetPos.x - customData.lastTargetPos.x) / scaledSSize.x > (data.xMovement ? Options.CameraStopDistance : Options.CameraStartDistance);
                if (data.xMovement)
                    delta.x = Plugin.LerpAndTickWithStop(cam.lastPos.x, targetPos.x, moveSpeed, moveSpeed * 0.005f * moveScaleSize.x, Options.CameraStopDistance * moveScaleSize.x) - cam.lastPos.x;

                data.yMovement = Mathf.Abs(targetPos.y - cam.lastPos.y) / moveScaleSize.y > (data.yMovement ? Options.CameraStopDistance : Options.CameraStartDistance);
                if (data.yMovement)
                    delta.y = Plugin.LerpAndTickWithStop(cam.lastPos.y, targetPos.y, moveSpeed, moveSpeed * 0.005f * moveScaleSize.y, Options.CameraStopDistance * moveScaleSize.y) - cam.lastPos.y;

                Vector2 maxDelta = customData.lastDelta; //not really used anymore, but the code is kept just in case
                maxDelta.x = Mathf.Abs(maxDelta.x) + Mathf.Abs(Options.CameraMaxAcceleration * delta.x);
                maxDelta.y = Mathf.Abs(maxDelta.y) + Mathf.Abs(Options.CameraMaxAcceleration * delta.y);
                delta.Set(Mathf.Clamp(delta.x, -maxDelta.x, maxDelta.x), Mathf.Clamp(delta.y, -maxDelta.y, maxDelta.y));

                cam.pos = cam.lastPos + delta;
                customData.lastDelta = delta;
            }
            customData.lastPos = cam.pos;
            //customData.lastTargetPos = origTargetPos;

        }
        catch (Exception ex)
        {
            Plugin.Error(ex);
            self._room_camera.pos = self._room_camera.lastPos; //don't move
        }
    }

    private static void IL_CheckBorders(ILContext il)
    {
        ILCursor c = new(il);
        if (c.TryGotoNext(MoveType.After, x => x.MatchLdcR4(18)))
        {
            //c.Next.Operand = 0f;
            //c.Emit(OpCodes.Pop);
            c.EmitDelegate((float f) => YBorderSize());
            Plugin.Log("Successful SBCameraScroll CheckBorders IL hook");
        }
    }

    #endregion

    #region Helpers
    public static Vector2 GetSBPlayerPos(RoomCamera cam)
    {
        var fields = cam.GetFields();
        return PlayerPosToScreenPos(cam, fields.on_screen_position);
    }

    private static Vector2 PlayerPosToScreenPos(RoomCamera cam, Vector2 pos)
    {
        Vector2 half = new(0.5f, 0.5f);
        return half + (pos - cam.pos) / cam.sSize; //SB's position is offset by half of sSize, for some reason
    }
    private static Vector2 ScreenPosToPlayerPos(RoomCamera cam, Vector2 pos)
    {
        Vector2 half = new(0.5f, 0.5f);
        return (pos - half) * cam.sSize + cam.pos;
    }


    public static bool UpdateSBCameraPos(RoomCamera cam, out Vector2 pos, Vector2 lastPos)
    {
        pos = new();

        //get position camera
        var fields = cam.GetFields();
        PositionTypeCamera positionCam = (fields.type_camera as PositionTypeCamera);
        if (positionCam == null && fields.type_camera is SwitchTypeCamera switchCam)
        {
            positionCam = switchCam._position_type_camera;//SwitchCamPositionCam.GetValue(switchCam) as PositionTypeCamera;
        }
        if (positionCam == null) return false;

        //save the true, original camera positions
        Vector2 origPos = cam.pos;
        Vector2 origLastPos = cam.lastPos;

        //hijack the camera positions to perform the calculation
        //cam.pos = ScreenPosToPlayerPos(cam, pos);
        cam.lastPos = ScreenPosToPlayerPos(cam, lastPos);

        //calculation
        //MoveCameraTowardsTarget.Invoke(positionCam, new object[] { fields.on_screen_position + positionCam.camera_offset, Vector2.zero });
        positionCam.Move_Camera_Towards_Target(fields.on_screen_position + positionCam.camera_offset, Vector2.zero);

        //restore original camera positions
        Vector2 tempPos = cam.pos;
        cam.pos = origPos;
        cam.lastPos = origLastPos;

        //set the pos using the data from the calculation
        pos = PlayerPosToScreenPos(cam, tempPos);
        //lastPos = PlayerPosToScreenPos(cam, cam.lastPos);

        return true;
    }

    public static Rect GetRoomRect(AbstractRoom room)
    {
        var fields = room.GetFields();
        return new Rect(fields.min_camera_position.x, fields.min_camera_position.y, fields.total_width, fields.total_height);
    }

    public static float YBorderSize() => Options.OverrideSBCamera == Options.SBCameraType.Default ? 18 : 2;

    #endregion

    #region Curves
    private static float _SmoothCurve2_11(float x, float s) => (x + s * x * x * x * (0.2f * x * x - 2 / 3)) / (1 - s * 7 / 15);
    public static float SmoothCurve2(float x, float s)
    {
        x = x + x - 1;
        return (_SmoothCurve2_11(x, s) + 1) * 0.5f;
    }
    private static float _DerivSmoothCurve2_11(float x, float s) => (1 + s * x * x * (x * x - 2)) / (1 - s * 7 / 15);
    public static float DerivSmoothCurve2(float x, float s)
    {
        x = x + x - 1;
        return (_DerivSmoothCurve2_11(x, s) + 1) * 0.5f;
    }
    //(1 + s * x*x*(x*x - 2)) / (1 - s*7/15) = l/m
    //x = 0
    //1 / (1 - s*7/15) = l/m
    //1 = l/m * (1 - s*7/15)
    //m/l = 1 - s*7/15
    //s*7/15 = 1 - m/l
    //s = 15/7 * (1 - m/l)
    public static float ProperSmoothFac(float camArea, float playerArea) => (1 - camArea / playerArea) * 15.0f / 7.0f;
    #endregion

}