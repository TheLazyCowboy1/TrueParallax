# TrueParallax

The mod can be downloaded by copying the "ModFiles" folder into your mods folder - although I suggest renaming it to TrueParallax or similar just to make it easier to identify.  


Fun fact: I think the formal term for this method of parallax is "Parallax Occlusion Mapping." This is likely more advanced than standard POM, though.  

Known Issues:
* Some level elements (like poles) can disappear when stepSize is extremely low and TwoLayers is enabled. This does not happen when Dynamic Optimization is enabled.
* FixBackgroundJitter doesn't work right with PivotDepth or with SBCameraScroll's zoom feature.
* SBCameraScroll's zoom feature has several shader bugs (black pixels around creatures; levTex mis-alignments in vanilla shaders) not caused by my mod.