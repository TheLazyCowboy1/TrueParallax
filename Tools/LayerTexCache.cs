using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TrueParallax.Tools;

public class LayerTexCache
{

    public KeyValuePair<string, RenderTexture>[] array;
    public Material mat;
    public RoomCamera cam;
    private int size;
    public int Size { get => size; set => Resize(value); }

    public LayerTexCache(int size, Material material, RoomCamera camera)
    {
        this.size = size;
        array = new KeyValuePair<string, RenderTexture>[size];
        mat = material;
        cam = camera;
    }

    public void Resize(int newSize)
    {
        if (newSize == size) return; //nothing to do

        if (newSize < size)
        {
            //since size is decreasing, release the textures we are removing
            for (int i = newSize; i < size; i++)
                array[i].Value?.Release();
        }

        Array.Resize(ref array, newSize);
        size = newSize;
    }

    public void Clear()
    {
        Resize(0);
        mat = null;
        cam = null;
    }

    public RenderTexture First => size > 0 ? array[0].Value : null;

    //public RenderTexture GetTexture(string key, Texture levelTex)
    public RenderTexture GetOrCreateTexture()
    {
        string key = cam.room.abstractRoom.name + ":" + cam.currentCameraPosition;
        Texture levelTex = LevTex(cam);

        //look for a texture with this key
        int idx = -1;
        for (int i = 0; i < size; i++)
        {
            if (array[i].Key == key)
            {
                idx = i;
                break;
            }
        }

        if (idx > 0) //found the texture in the cache, but need to move it forward
        {
            var foundTex = array[idx]; //pop out the correct texture
            for (int i = idx; i > 0; i--) //shift the array forward to fill in the gap and free up index 0
                array[i] = array[i - 1];
            array[0] = foundTex; //put it in index 0
            Plugin.Log($"Found Layer2Tex at index {idx}: " + array[0].Key, 2);
        }
        else if (idx < 0) //did NOT find the texture, so need to generate it
        {
            idx = size - 1; //the last texture in the array
            int width = levelTex.width, height = levelTex.height;
            RenderTexture tex = array[idx].Value ?? CreateRenderTex(width, height); //create a new texture if one doesn't exist
            if (tex.width != width || tex.height != height) //fix dimensions
            {
                tex.width = width;
                tex.height = height;
            }
            /*
            Stopwatch sw = new();
            sw.Start();
            Graphics.Blit(levelTex, tex, mat);
            sw.Stop();
            Plugin.Log("Stopwatch results: " + sw.ElapsedMilliseconds + ":" + sw.ElapsedTicks, 3);
            */
            CommandBuffer cmd = new();
            cmd.Blit(levelTex, tex, mat);
            Graphics.ExecuteCommandBufferAsync(cmd, ComputeQueueType.Default);

            //shift array forward to make room at index 0
            for (int i = idx; i > 0; i--)
                array[i] = array[i - 1];

            array[0] = new(key, tex);
            Plugin.Log("Generated new Layer2Tex: " + key);
        }

        return FixRenderTex(array[0].Value, levelTex);
    }

    //Weird SBCameraScroll practices...
    private static Texture LevTex(RoomCamera self) => Plugin.SBCameraScrollEnabled ? self.levelGraphic?._atlas?.texture : self.levelTexture;

    //exists just in case; hopefully it won't be used
    private RenderTexture FixRenderTex(RenderTexture tex, Texture levelTex)
    {
        if (tex.width == levelTex.width && tex.height == levelTex.height)
            return tex; //it's already fine

        tex.width = levelTex.width;
        tex.height = levelTex.height;
        Graphics.Blit(levelTex, tex, mat); //regenerate the texture
        Plugin.Log("Layer2Tex was the wrong size!! Regenerated texture.", 1);
        return tex;
    }

    private static RenderTexture CreateRenderTex(int width, int height) => new(width, height, 0, DefaultFormat.LDR) { filterMode = 0 };

}
