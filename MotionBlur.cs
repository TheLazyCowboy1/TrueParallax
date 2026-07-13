using UnityEngine;

namespace TrueParallax;

public class MotionBlur : MonoBehaviour
{
    RenderTexture lastImg;
    Material mat = new(Plugin.CustomBlendShader);
    public void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (Options.MotionBlur <= 0 || Plugin.SharpenerEnabled)
        {
            lastImg?.Release();
            lastImg = null;
            Graphics.CopyTexture(source, destination);
            return;
        }

        if (lastImg != null && lastImg.width == source.width && lastImg.height == source.height)
        {
            mat.SetTexture("LZC_BlendWith", lastImg);
            mat.SetFloat("LZC_CustomBlend", Options.MotionBlur);
            Graphics.Blit(source, destination, mat);
        }
        else
        {
            lastImg = new(source);
            Plugin.Log($"lastImg RenderTexture size = {lastImg.width}x{lastImg.height}");
            Graphics.CopyTexture(source, destination);
        }

        //Graphics.CopyTexture(source, lastImg);
        Graphics.CopyTexture(destination, lastImg);
    }
}
