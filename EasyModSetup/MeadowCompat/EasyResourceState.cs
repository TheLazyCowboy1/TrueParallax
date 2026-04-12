using RainMeadow;
using System;
using System.Linq;
using System.Reflection;

namespace EasyModSetup.MeadowCompat;

/// <summary>
/// Used to easily attach data to OnlineResources.
/// Create a class that extends this EasyResourceState.
/// Use AttachTo to determine specify whether this data will be attached to a resource.
/// Create a bunch of variables with the OnlineField attribute. These will be synced between users.
/// Assign the values of the variables in WriteTo.
/// Read the values in ReadTo.
/// And just like that, you've synced data for your resource!
/// </summary>
public abstract class EasyResourceState : OnlineResource.ResourceData.ResourceDataState
{
    /// <summary>
    /// If this returns true, then this data will automatically be attached to the resource when it is created.
    /// </summary>
    /// <param name="resource">The resource to attach the data to</param>
    /// <returns>Whether the data should be automatically attached to this resource</returns>
    public abstract bool AttachTo(OnlineResource resource);

    /// <summary>
    /// The resource OWNER calls this function. Use this to ASSIGN the values of your OnlineFields.
    /// </summary>
    /// <param name="data">Probably totally useless for you to reference. Just ignore it, I guess.</param>
    /// <param name="resource">The resource the data is being assigned to</param>
    public abstract void WriteTo(OnlineResource.ResourceData data, OnlineResource resource);


    public override Type GetDataType() => typeof(EasyResourceData<>).MakeGenericType(this.GetType());

    private class EasyResourceData<T> : OnlineResource.ResourceData where T : EasyResourceState, new()
    {
        public EasyResourceData() : base() { }
        public EasyResourceData(OnlineResource resource) : base() { } //required functionality

        public override ResourceDataState MakeState(OnlineResource resource)
        {
            T t = new();
            t.WriteTo(this, resource);
            return t;
        }

        public override string ToString() => $"EasyResourceData<{typeof(T).FullName}>";
    }


    #region Hooks
    private static bool HooksApplied = false;
    private static Type[] RegisteredTypes;
    public static void ApplyHooks()
    {
        if (HooksApplied) return;

        RegisteredTypes = Assembly.GetExecutingAssembly().GetTypesSafely().Where(t => t.IsSubclassOf(typeof(EasyResourceState))).ToArray();
        if (RegisteredTypes.Length > 0) //only add if there's a use for it
            OnlineResource.OnAvailable += OnlineResource_OnAvailable;

        HooksApplied = true;
    }
    public static void RemoveHooks()
    {
        if (!HooksApplied) return;

        if (RegisteredTypes.Length > 0)
            OnlineResource.OnAvailable -= OnlineResource_OnAvailable;

        HooksApplied = false;
    }
    private static void OnlineResource_OnAvailable(OnlineResource r)
    {
        foreach (Type t in RegisteredTypes)
        {
            try
            {
                EasyResourceState s = Activator.CreateInstance(t) as EasyResourceState; //this feels so horrible to do
                if (s.AttachTo(r))
                {
                    var d = r.AddData(s.MakeData(r));
                    SimplerPlugin.Log($"Attached data {d} to resource {r}");
                }
            }
            catch (Exception ex) { SimplerPlugin.Error(ex); }
        }
    }
    #endregion

}