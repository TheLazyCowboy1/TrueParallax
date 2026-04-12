using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrueParallax.Tools;
using UnityEngine;

namespace TrueParallax;

public class CameraData
{
    public static ResizableArray<CameraData> list = new(2);

    public Vector2 camPos;
    public FSprite sprite;
    public bool needSetConstants;
    public RenderTexQueue layer2Textures = new();

    public Material SpriteMaterial => sprite?._renderLayer?._material;
}

public static class CameraEXT
{
    public static CameraData GetData(this RoomCamera cam) => CameraData.list[cam.cameraNumber];
    public static bool TryGetData(this RoomCamera cam, out CameraData data) => CameraData.list.TryGetValue(cam.cameraNumber, out data);
    /// <summary>
    /// Instantiates the CameraData if necessary.
    /// </summary>
    /// <returns>
    /// The existing CameraData for this camera number if it exists;
    /// otherwise, it creates a new CameraData instance.
    /// </returns>
    public static CameraData InstantiateData(this RoomCamera cam)
    {
        if (CameraData.list.ContainsKey(cam.cameraNumber)) return CameraData.list[cam.cameraNumber]; //already exists; doesn't need to be initialized
        CameraData data = new();
        CameraData.list.Add(cam.cameraNumber, data);
        return data;
    }

}
