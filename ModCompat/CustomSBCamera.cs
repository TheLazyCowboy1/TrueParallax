using SBCameraScroll;
using static SBCameraScroll.RoomCameraMod;

namespace TrueParallax.ModCompat;

public class CustomSBCamera : PositionTypeCamera
{
    private readonly RoomCamera _room_camera;
    private readonly RoomCameraFields _room_camera_fields;

    public CustomSBCamera(RoomCamera room_camera, RoomCameraFields room_camera_fields) : base(room_camera, room_camera_fields)
    {
        //_room_camera = room_camera;
        //_room_camera_fields = room_camera_fields;
    }

    public new void Reset()
    {
        UpdateOnScreenPosition(_room_camera);
        CheckBorders(_room_camera, ref _room_camera_fields.on_screen_position); // do not move past room boundaries

        // center camera on player
        _room_camera.lastPos = _room_camera_fields.on_screen_position;
        _room_camera.pos = _room_camera_fields.on_screen_position;
    }

    public new void Update()
    {
        if (_room_camera.followAbstractCreature == null) return;
        if (_room_camera.room == null) return;

        if (!_room_camera.TryGetData(out CameraData data))
        {
            Reset();
            return;
        }

        UpdateOnScreenPosition(_room_camera);

        _room_camera.pos += (data.CamPos - data.lastCamPos) * _room_camera.sSize; //idiotically simple; will it actually work, lol?
        CheckBorders(_room_camera, ref _room_camera.pos);
    }
}
