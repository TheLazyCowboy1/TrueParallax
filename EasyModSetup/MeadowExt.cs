using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace EasyModSetup;

/// <summary>
/// Used as a middle-man for interfacing with Rain Meadow. Useful for soft-compatibility.
/// </summary>
public static class MeadowExt
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnline(this AbstractPhysicalObject apo)
    {
        if (!SimplerPlugin.RainMeadowEnabled) return false; //don't even try if it won't work
        try
        {
            return MeadowCompat.MeadowExtCompat.IsOnline(apo);
        }
        catch (Exception ex) { SimplerPlugin.Error(ex); }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnline(this PhysicalObject obj) => IsOnline(obj.abstractPhysicalObject);


    public static void ApplyHooks()
    {
        try
        {
            if (SimplerPlugin.RainMeadowEnabled)
            {
                MeadowCompat.EasyResourceState.ApplyHooks();
                MeadowCompat.EasyEntityState.ApplyHooks();
                if (SimplerPlugin.ConfigOptions is AutoConfigOptions //if there is an AutoConfigOptions that contains AutoSync fields
                    && SimplerPlugin.ConfigOptions.GetType().GetStaticFieldsSafely().Any(f => f.GetCustomAttribute<AutoSync>() != null))
                {
                    MeadowCompat.AutoConfigLobbyHooks.ApplyHooks(); //update it when owning or leaving the lobby
                }
            }
        }
        catch (Exception ex) { SimplerPlugin.Error(ex); }
    }
    public static void RemoveHooks()
    {
        try
        {
            if (SimplerPlugin.RainMeadowEnabled)
            {
                MeadowCompat.EasyResourceState.RemoveHooks();
                MeadowCompat.EasyEntityState.RemoveHooks();
                if (SimplerPlugin.ConfigOptions is AutoConfigOptions)
                    MeadowCompat.AutoConfigLobbyHooks.RemoveHooks();
            }
        }
        catch (Exception ex) { SimplerPlugin.Error(ex); }
    }


    //Stolen from Rain Meadow. Credits go to whoever wrote it there
    public static Type[] GetTypesSafely(this Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e) // happens often with soft-dependencies, did you know
        {
            return e.Types.Where(x => x != null).ToArray();
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldInfo[] GetStaticFieldsSafely(this Type type) //the key is BindingFlags.DeclaredOnly
    {
        try
        {
            return type.GetFields(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
        } catch { SimplerPlugin.Error($"Error loading type {type}"); }
        return new FieldInfo[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PropertyInfo[] GetStaticPropertiesSafely(this Type type)
    {
        try
        {
            return type.GetProperties(BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic);
        } catch { SimplerPlugin.Error($"Error loading type {type}"); }
        return new PropertyInfo[0];
    }

}
