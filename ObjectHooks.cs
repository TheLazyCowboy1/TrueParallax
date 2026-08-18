using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace TrueParallax;

public partial class Plugin
{

    private bool MoveHUDToAfterParallaxContainer = false;

    private void SpriteLeaser_ctor(On.RoomCamera.SpriteLeaser.orig_ctor orig, RoomCamera.SpriteLeaser self, IDrawable obj, RoomCamera rCam)
    {
        MoveHUDToAfterParallaxContainer = rCam.SpriteLayerIndex.ContainsKey(AFTERPARALLAXCONTAINER);
        orig(self, obj, rCam);
        MoveHUDToAfterParallaxContainer = false;
    }
    private FContainer RoomCamera_ReturnFContainer(On.RoomCamera.orig_ReturnFContainer orig, RoomCamera self, string layerName)
    {
        try
        {
            if (MoveHUDToAfterParallaxContainer)
            {
                int idx = self.SpriteLayerIndex[layerName];
                int parallaxIdx = self.SpriteLayerIndex[AFTERPARALLAXCONTAINER];
                if (idx > parallaxIdx)
                {
                    return self.SpriteLayers[parallaxIdx];
                }
            }
        } catch (Exception ex) { Error(ex); }
        return orig(self, layerName);
    }


    //Move rain to parallax container, because it distorts the screen
    private void RoomRain_AddToContainer(On.RoomRain.orig_AddToContainer orig, RoomRain self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
        orig(self, sLeaser, rCam, newContatiner);

        try
        {
            sLeaser.sprites[0].RemoveFromContainer();
            rCam.ReturnFContainer(PARALLAXCONTAINER).AddChildAtIndex(sLeaser.sprites[0], 1); //make sure it is after parallax but BEFORE full-screen effects
            Log($"Moved rain in room {self.room.abstractRoom.name} into parallax container.", 2);
        }
        catch (Exception ex) { Error(ex); }
    }

    //Stop distortion from messing with camera please
    private void CellDistortion_InitiateSprites(On.MoreSlugcats.CellDistortion.orig_InitiateSprites orig, MoreSlugcats.CellDistortion self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        //move out of Bloom container and into parallax container
        try
        {
            sLeaser.sprites[0].RemoveFromContainer();
            rCam.ReturnFContainer(PARALLAXCONTAINER).AddChild(sLeaser.sprites[0]);
        }
        catch (Exception ex) { Error(ex); }
    }

    //Optionally disables decals flickering
    private int CustomDecal_GetIdealGridDiv(On.CustomDecal.orig_GetIdealGridDiv orig, CustomDecal self)
    {
        try
        {
            if (Options.FixDecalFlickering)
            {
                for (int i = 0; i < self.quad.Length; i++)
                    self.quad[i] *= 2; //scale up so that we get a bigger gridDiv

                int val = orig(self);

                for (int i = 0; i < self.quad.Length; i++)
                    self.quad[i] *= 0.5f; //scale back down

                return val;
            }
        }
        catch (Exception ex) { Error(ex); }

        return orig(self);
    }
    private void CustomDecal_UpdateVerts(On.CustomDecal.orig_UpdateVerts orig, CustomDecal self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        try
        {
            if (!Options.FixDecalFlickering)
                return;
            if (sLeaser.sprites[0] is not TriangleMesh mesh)
                return;
            for (int i = 0; i < mesh.verticeColors.Length; i++)
            {
                Color c = mesh.verticeColors[i];

                //offset vertices
                self.verts[i].y -= 40.0f * c.b * (noise.snoise(new float2(self.verts[i].x, self.verts[i].y * 0.1f) * 0.015f) + 0.75f);

                c.b = 0; //disable blue channel == disable erosion
                mesh.verticeColors[i] = c;
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    private void GateKarmaGlyph_InitiateSprites(On.GateKarmaGlyph.orig_InitiateSprites orig, GateKarmaGlyph self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        try
        {
            FContainer container = rCam.ReturnFContainer(AFTERPARALLAXCONTAINER);
            for (int i = 1; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                container.AddChild(sLeaser.sprites[i]);
            }
        }
        catch (Exception ex) { Error(ex); }
    }

    private void WeaverThread_InitiateSprites(On.Watcher.WeaverThread.orig_InitiateSprites orig, Watcher.WeaverThread self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
    {
        orig(self, sLeaser, rCam);

        try
        {
            self.AddToContainer(sLeaser, rCam, rCam.ReturnFContainer(AFTERPARALLAXCONTAINER));
        }
        catch (Exception ex) { Error(ex); }
    }

    private void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
    {
        orig(self, sLeaser, rCam, newContatiner);

        try
        {
            int foregroundIdx = rCam.SpriteLayerIndex["Foreground"];
            int parallaxIdx = rCam.SpriteLayerIndex[AFTERPARALLAXCONTAINER];
            FContainer afterParallaxContainer = rCam.SpriteLayers[parallaxIdx];
            foreach (FSprite sprite in sLeaser.sprites)
            {
                int spriteIdx = rCam.SpriteLayers.IndexOf(sprite.container);
                if (spriteIdx < 0) continue; //problem????
                if (spriteIdx > parallaxIdx) //move into AfterParallax container. This applies to Saint's ascension stuff
                {
                    sprite.RemoveFromContainer();
                    afterParallaxContainer.AddChild(sprite);
                }
                else if (spriteIdx >= foregroundIdx && (sprite.shader == null || sprite.shader == FShader.defaultShader))
                    sprite.shader = ForegroundCreatureFShader; //foreground creature part
            }
        }
        catch (Exception ex) { Error(ex); }
    }

}
