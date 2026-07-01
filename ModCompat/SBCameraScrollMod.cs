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

    private static void Hook_UpdateOnScreenPosition(Action<RoomCamera> orig, RoomCamera room_camera)
    {
        orig(room_camera);
        if (Options.AdjustSBCameraFac == 0)
            return;
        if (!room_camera.TryGetData(out CameraData data) || data.Inactive)
            return; //parallax isn't active

        Vector2 offset = Options.AdjustSBCameraFac * data.CalculateWarp(new(0.5f, 0.5f));
        room_camera.GetFields().on_screen_position += offset;
    }
}