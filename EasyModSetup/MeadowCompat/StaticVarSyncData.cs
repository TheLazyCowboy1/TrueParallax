using RainMeadow;

namespace EasyModSetup.MeadowCompat;

/// <summary>
/// Data added to lobbies in order to automatically sync AutoSync fields.
/// </summary>
public class StaticVarSyncData : EasyResourceState
{
    public override bool AttachTo(OnlineResource resource) => AutoSync.ShouldSync && resource is Lobby; //don't add resource if there's nothing to sync

    [OnlineField]
    public bool[] bools;
    [OnlineField]
    public int[] ints;
    [OnlineField]
    public float[] floats;
    [OnlineField]
    public string[] strings;

    public override void WriteTo(OnlineResource.ResourceData data, OnlineResource resource)
    {
        bools = AutoSync.GetSyncedVars<bool>();
        ints = AutoSync.GetSyncedVars<int>();
        floats = AutoSync.GetSyncedVars<float>();
        strings = AutoSync.GetSyncedVars<string>();
    }

    public override void ReadTo(OnlineResource.ResourceData data, OnlineResource resource)
    {
        AutoSync.SetSyncedVars(bools, ints, floats, strings);
    }
}