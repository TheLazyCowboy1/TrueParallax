using MonoMod.RuntimeDetour;
using RainMeadow;
using System;
using System.Linq;
using System.Reflection;

namespace EasyModSetup.MeadowCompat;

/// <summary>
/// Used to easily attach data to OnlineEntitys.
/// Create a class that extends this EasyEntityState.
/// Use AttachTo to determine specify whether this data will be attached to an entity.
/// Create a bunch of variables with the OnlineField attribute. These will be synced between users.
/// Assign the values of the variables in WriteTo.
/// Read the values in ReadTo.
/// And just like that, you've synced data for your entity!
/// </summary>
public abstract class EasyEntityState : OnlineEntity.EntityData.EntityDataState
{
    /// <summary>
    /// If this returns true, then this data will automatically be attached to the entity when it is created.
    /// </summary>
    /// <param name="entity">The entity to attach the data to</param>
    /// <returns>Whether the data should be automatically attached to this entity</returns>
    public abstract bool AttachTo(OnlineEntity entity);

    /// <summary>
    /// The entity OWNER calls this function. Use this to ASSIGN the values of your OnlineFields.
    /// </summary>
    /// <param name="data">Probably totally useless for you to reference. Just ignore it, I guess.</param>
    /// <param name="onlineEntity">The entity the data is being assigned to</param>
    public abstract void WriteTo(OnlineEntity.EntityData data, OnlineEntity onlineEntity);

    public override Type GetDataType() => typeof(EasyEntityData<>).MakeGenericType(this.GetType());

    private class EasyEntityData<T> : OnlineEntity.EntityData where T : EasyEntityState, new()
    {
        public EasyEntityData() : base() { }
        public EasyEntityData(OnlineEntity entity) : base() { } //required functionality

        public override EntityDataState MakeState(OnlineEntity entity, OnlineResource inResource)
        {
            T t = new();
            t.WriteTo(this, entity);
            return t;
        }

        public override string ToString() => $"EasyEntityData<{typeof(T).FullName}>";
    }


    #region Hooks
    private static bool HooksApplied = false;
    private static Type[] RegisteredTypes;
    private static Hook EntityHook;
    public static void ApplyHooks()
    {
        if (HooksApplied) return;

        RegisteredTypes = Assembly.GetExecutingAssembly().GetTypesSafely().Where(t => t.IsSubclassOf(typeof(EasyEntityState))).ToArray();
        if (RegisteredTypes.Length > 0) //only add if there's a use for it
            EntityHook = new(typeof(OnlineGameMode).GetMethod(nameof(OnlineGameMode.NewEntity)), OnlineEntity_OnAvailable);

        HooksApplied = true;
    }
    public static void RemoveHooks()
    {
        if (!HooksApplied) return;

        EntityHook?.Undo();

        HooksApplied = false;
    }
    private static void OnlineEntity_OnAvailable(Action<OnlineGameMode, OnlineEntity, OnlineResource> orig, OnlineGameMode self, OnlineEntity e, OnlineResource r)
    {
        orig(self, e, r);
        foreach (Type t in RegisteredTypes)
        {
            try
            {
                EasyEntityState s = Activator.CreateInstance(t) as EasyEntityState; //this feels so horrible to do
                if (s.AttachTo(e))
                {
                    var d = e.AddData(s.MakeData(e));
                    SimplerPlugin.Log($"Attached data {d} to entity {e}");
                }
            }
            catch (Exception ex) { SimplerPlugin.Error(ex); }
        }
    }
    #endregion

}