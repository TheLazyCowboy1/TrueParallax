using TrueParallax.Tools;
using UnityEngine;

namespace TrueParallax;

public class CameraData
{
    public static ResizableArray<CameraData> list = new(2);

    public Vector2 camPos;

    public float currentWarp = Options.Warp;
    public Vector2 BackgroundShift { get => currentWarp * (camPos - new Vector2(0.5f, 0.5f)); }

    public FSprite sprite;
    public bool needSetConstants;
    public LayerTexCache layer2Textures;
    public int idx;

    public Material SpriteMaterial => sprite?._renderLayer?._material;

    public CameraData(RoomCamera camera)
    {
        layer2Textures = new(0, Plugin.ThicknessMapMaterial, camera);
        idx = camera.cameraNumber;
        list.Add(idx, this);
    }

    public void Clear()
    {
        sprite = null;
        layer2Textures.Clear();
        layer2Textures = null;
        list.Remove(idx);
    }
}

public static class CameraEXT
{
    public static CameraData GetData(this RoomCamera cam)
        => CameraData.list[cam.cameraNumber];
    public static bool TryGetData(this RoomCamera cam, out CameraData data)
        => CameraData.list.TryGetValue(cam.cameraNumber, out data);
    /// <summary>
    /// Instantiates the CameraData if necessary.
    /// </summary>
    /// <returns>
    /// The existing CameraData for this camera number if it exists;
    /// otherwise, it creates a new CameraData instance.
    /// </returns>
    public static CameraData CreateData(this RoomCamera cam)
        => new(cam);
    /*{
        if (CameraData.list.ContainsKey(cam.cameraNumber)) return CameraData.list[cam.cameraNumber]; //already exists; doesn't need to be initialized
        CameraData data = new(cam);
        CameraData.list.Add(cam.cameraNumber, data);
        return data;
    }*/

    public static RenderTexture GetLayer2Tex(this RoomCamera cam)
        => TryGetData(cam, out CameraData data) ? data.layer2Textures.GetOrCreateTexture() : null;

}
