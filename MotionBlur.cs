using System;
using UnityEngine;

namespace TrueParallax;

public class MotionBlur : MonoBehaviour
{
    RenderTexture lastImg;
    Material mat = new(Plugin.CustomBlendShader);
    //public void OnRenderImage(RenderTexture source, RenderTexture destination)
    public void OnPostRender()
    {
        try
        {
            if (Options.MotionBlur <= 0)
            {
                lastImg?.Release();
                lastImg = null;
                return;
            }

            Texture camImage = Futile.instance._cameraImage.texture;
            if (camImage is null)
            {
                return; //Futile isn't set up yet ?
            }

            if (camImage is not RenderTexture destination)
            {
                Plugin.Log("NOT A RENDER TEXTURE!!!");
                Options.MotionBlur = 0;
                return;
            }

            if (lastImg != null && lastImg.width == destination.width && lastImg.height == destination.height)
            {
                mat.SetTexture("LZC_BlendWith", lastImg);
                mat.SetFloat("LZC_CustomBlend", Options.MotionBlur);

                RenderTexture source = RenderTexture.GetTemporary(destination.descriptor);
                Graphics.Blit(destination, source);
                Graphics.Blit(source, destination, mat);
                RenderTexture.ReleaseTemporary(source);
            }
            else //lastImg is not accurate; create a new one
            {
                lastImg?.Release();
                lastImg = new(destination);
                Plugin.Log($"lastImg RenderTexture size = {lastImg.width}x{lastImg.height}");
            }
            
            Graphics.Blit(destination, lastImg);
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }
}
