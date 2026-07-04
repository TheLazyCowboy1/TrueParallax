using MonoMod.RuntimeDetour;
using RWCustom;
using SBCameraScroll;
using System;
using System.Security;
using System.Security.Permissions;
using UnityEngine;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TrueParallax.ModCompat;

public static class SBCameraScrollMod
{
    private static Hook PositionHook = null;
    private static Hook PositionCamUpdateHook = null;

    public static void ApplyHooks()
    {
        PositionHook = new((Delegate)RoomCameraMod.UpdateOnScreenPosition, Hook_UpdateOnScreenPosition);
        PositionCamUpdateHook = new(typeof(PositionTypeCamera).GetMethod(nameof(PositionTypeCamera.Update)), Hook_PositionCameraUpdate);
    }
    public static void RemoveHooks()
    {
        PositionHook?.Undo();
        PositionCamUpdateHook?.Undo();
    }

    private static string LastRoomName;
    private static Vector2 RoomCenter = new(), AreaScale = new();

    private static void Hook_UpdateOnScreenPosition(Action<RoomCamera> orig, RoomCamera room_camera)
    {
        orig(room_camera);
        if (Options.AdjustSBCameraFac == 0)
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

            float expand = Mathf.Max(0, Options.AdjustSBCameraFac * 2 * data.totalWarp * data.DepthCurve(5f / 30f));
            Vector2 newMovementArea = movementArea + new Vector2(expand, expand);
            AreaScale = movementArea / newMovementArea;
            if (movementArea.x <= 0) AreaScale.x = 1; //don't scale x if the camera can't move horizontally anyway
            if (movementArea.y <= 0) AreaScale.y = 1;
        }
    }

    private static void Hook_PositionCameraUpdate(Action<PositionTypeCamera> orig, PositionTypeCamera self)
    {
        if (!Options.CustomSBCamera || !self._room_camera.TryGetData(out CameraData data))
        {
            orig(self);
            return;
        }

        RoomCamera cam = self._room_camera;
        Vector2 onScreenPosition = cam.GetFields().on_screen_position;
        RoomCameraMod.UpdateOnScreenPosition(cam); //done just in case

        float moveSpeed = Options.CameraMoveSpeed;
        Vector2? critPos = Plugin.GetCritPos(cam, data, Options.AlwaysCentered || Options.UseSBPlayerPos, moveSpeed);
        if (critPos == null)
        {
            cam.pos = cam.lastPos; //don't move
            return;
        }
        Vector2 targetPos = critPos.Value;

        //scale around RoomCenter
        /*
        if (Options.AdjustSBCameraFac != 0)
        {
            CalcRoomScaleFac(cam, data);
            targetPos = (targetPos - RoomCenter) * AreaScale + RoomCenter;
        }

        targetPos -= 0.5f * cam.sSize; //because we want player to be in center, not top corner
        */

        //apply borders
        var fields = cam.room.abstractRoom.GetFields();
        Vector2 roomSize = new(fields.total_width, fields.total_height);
        Vector2 corner = fields.min_camera_position;
        Vector2 border = new(Options.CustomCameraBorderPixels, Options.CustomCameraBorderPixels);

        Vector2 fracPos = (targetPos - corner - border) / (roomSize - border - border);

        //apply SmoothCurve
        if (Options.CustomCameraCurve != 0)
        {
            fracPos.Set(Plugin.SmoothCurve(Mathf.Clamp01(fracPos.x), Options.CustomCameraCurve), Plugin.SmoothCurve(Mathf.Clamp01(fracPos.y), Options.CustomCameraCurve));
        }
        targetPos = fracPos * (roomSize - cam.sSize) + corner;

        RoomCameraMod.CheckBorders(cam, ref targetPos); //very important step I forgot, lol

        //Actually set position
        if (cam.pos == onScreenPosition && cam.lastPos == onScreenPosition) //camera position was probably just reset
        {
            cam.pos = targetPos; //no smoothing
        }
        else
        {
            data.xMovement = Mathf.Abs(targetPos.x - cam.lastPos.x) / cam.sSize.x > (data.xMovement ? Options.CameraStopDistance : Options.CameraStartDistance);
            if (data.xMovement)
                cam.pos.x = Custom.LerpAndTick(cam.lastPos.x, targetPos.x, moveSpeed, moveSpeed * 0.005f * cam.sSize.x);
            else
                cam.pos.x = cam.lastPos.x;

            data.yMovement = Mathf.Abs(targetPos.y - cam.lastPos.y) / cam.sSize.y > (data.yMovement ? Options.CameraStopDistance : Options.CameraStartDistance);
            if (data.yMovement)
                cam.pos.y = Custom.LerpAndTick(cam.lastPos.y, targetPos.y, moveSpeed, moveSpeed * 0.005f * cam.sSize.y);
            else
                cam.pos.y = cam.lastPos.y;
        }

    }


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

    //private readonly static BindingFlags BindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    //private readonly static MethodInfo MoveCameraTowardsTarget = typeof(PositionTypeCamera).GetMethod("Move_Camera_Towards_Target", BindingFlags);
    //private readonly static FieldInfo SwitchCamPositionCam = typeof(SwitchTypeCamera).GetField("_position_type_camera", BindingFlags);
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

}