using MonoMod.RuntimeDetour;
using SBCameraScroll;
using System;
using UnityEngine;

namespace TrueParallax.ModCompat;

public static class SBCameraScrollMod
{
    private static Hook PositionHook = null;

    public static void ApplyHooks()
    {
        PositionHook = new((Delegate)SBCameraScroll.RoomCameraMod.UpdateOnScreenPosition, Hook_UpdateOnScreenPosition);
    }
    public static void RemoveHooks()
    {
        PositionHook.Undo();
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

        string roomName = room_camera.room.abstractRoom.name;
        if (roomName != LastRoomName) //only calculate this if it has changed
        {
            //calculate camera movement box
            var roomFields = room_camera.room.abstractRoom.GetFields();

            Vector2 roomSize = new(roomFields.total_width, roomFields.total_height);
            RoomCenter = roomFields.min_camera_position + 0.5f * roomSize;
            Vector2 movementArea = roomSize - room_camera.sSize;

            float expand = Mathf.Max(0, Options.AdjustSBCameraFac * 2 * data.totalWarp * data.DepthCurve(5f / 30f));
            Vector2 newMovementArea = movementArea + new Vector2(expand, expand);
            AreaScale = movementArea / newMovementArea;
            if (movementArea.x <= 0) AreaScale.x = 1; //don't scale x if the camera can't move horizontally anyway
            if (movementArea.y <= 0) AreaScale.y = 1;
        }

        //scale around RoomCenter by AreaScale
        var cameraFields = room_camera.GetFields();
        cameraFields.on_screen_position = (cameraFields.on_screen_position - RoomCenter) * AreaScale + RoomCenter;
    }
}