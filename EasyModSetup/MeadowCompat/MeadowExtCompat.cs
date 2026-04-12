using RainMeadow;

namespace EasyModSetup.MeadowCompat;

/// <summary>
/// It is recommended that you use separate files like these to reference anything from RainMeadow, for soft-compatibility purposes.
/// </summary>
public static class MeadowExtCompat
{
    //public static bool IsLocal(AbstractPhysicalObject apo) => apo.IsLocal();
    public static bool IsOnline(AbstractPhysicalObject apo)
        => OnlineManager.lobby != null && OnlinePhysicalObject.map.TryGetValue(apo, out OnlinePhysicalObject opo) && !opo.owner.isMe;
}
