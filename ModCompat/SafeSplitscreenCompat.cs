using System;
using UnityEngine;

namespace TrueParallax.ModCompat;

public static class SafeSplitscreenCompat
{
    /// <summary>
    /// Copied from SplitScreenCoop.cs
    /// </summary>
    public static Vector2[] camOffsets = new Vector2[] { new Vector2(0, 0), new Vector2(32000, 0), new Vector2(0, 32000), new Vector2(32000, 32000) }; // one can dream

    /// <summary>
    /// From SplitScreenCoop.RoomCamera_ctor1
    /// </summary>
    public static void OffsetContainer(RoomCamera camera, FContainer container)
    {
        try
        {
            if (Plugin.SplitScreenEnabled)
            {
                container.SetPosition(camOffsets[camera.cameraNumber]);
            }
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }
}
