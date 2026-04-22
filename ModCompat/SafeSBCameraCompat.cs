using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrueParallax.ModCompat;

public static class SafeSBCameraCompat
{
    public static void FixCameraZoom(RoomCamera self)
    {
        try
        {
            if (!Plugin.SBCameraScrollEnabled)
                return;
            if (!self.TryGetData(out CameraData data))
                return;

            data.sprite.width = self.sSize.x * self.SpriteLayers[0].scale;
            data.sprite.height = self.sSize.y * self.SpriteLayers[0].scale;
            data.sprite.scale = 1 / self.SpriteLayers[0].scale;
            Plugin.Log("Changed parallax sprite scale: " + data.sprite.scale, (data.sprite.scale == 1) ? 3 : 2);
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }
}
