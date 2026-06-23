# TrueParallax

Fun fact: I think the formal term for this method is "Parallax Occlusion Mapping." This is likely more advanced than standard POM, though.

Known Issues:
* Some level elements (like poles) can disappear when stepSize is extremely low and TwoLayers is enabled. This does not happen when Dynamic Optimization is enabled.
* Slugcat hands (when climbing vertical poles) are drawn after the level, so they are not included in PreLevelColorGrab.