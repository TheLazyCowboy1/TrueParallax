using TrueParallax.Tools;
using UnityEngine;

namespace TrueParallax;

public partial class CameraData
{
    public static ResizableArray<CameraData> list = new(2);

    public RoomCamera camera;

    public Vector2 lastCamPos;
    private Vector2 _camPos;
    public Vector2 CamPos { get => _camPos; set { lastCamPos = _camPos; posDirty = true; _camPos = value; } }

    public Vector2 critFollowOffset = new(0, 0);
    public Vector2 mouseOffset = new(0, 0);

    public bool posDirty = true;
    public float currentWarp = Options.Warp;
    private Vector2 _backgroundShift;
    public Vector2 BackgroundShift
    {
        get {
            if (posDirty)
            {
                _backgroundShift = -Options.BackgroundShift * CalculateWarp(new(0.5f, 0.5f));
                posDirty = true;
            }
            return _backgroundShift;
        }
    }
    public bool activeBackgroundScene = true; //only updated if Options.BackDepthForSceneOnly == true
    public float CurrentLayer30Depth => activeBackgroundScene ? 1.0f / Options.BackgroundDepth : 1;
    public float CurrentMaxProjection => Options.MaxProjection * (Options.IsActiveSuperAccurateThickness ? 1 : CurrentLayer30Depth);

    public FSprite sprite;
    public bool needSetConstants;
    public LayerTexCache layer2Textures;

    public Material SpriteMaterial => sprite?._renderLayer?._material;

    public CameraData(RoomCamera camera)
    {
        this.camera = camera;
        layer2Textures = new(0, Plugin.ThicknessMapMaterial, camera);
        list.Add(camera.cameraNumber, this);
    }

    public void Clear()
    {
        sprite = null;
        layer2Textures.Clear();
        layer2Textures = null;
        list.Remove(camera.cameraNumber);
        camera = null;
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
