Low-Poly Prison Guard
=====================

low-poly-prison-guard.glb is the guard character (3,328 triangles, flat-colour materials, no textures).

Source:   https://shuriken.studio/models/characters/low-poly-prison-guard  (downloaded 2026-11-24)
License:  Shuriken Studio free model license, version 2026-11-13: https://shuriken.studio/legal/models
          Free to use and change in any project, no credit needed. It may ship inside this game and
          its source, but must not be shared on its own or as part of an asset pack.
          The license page states the models are made with AI tools.

Importing .glb files needs Unity's glTFast package (com.unity.cloud.gltfast), which is listed in
Packages/manifest.json.

The model has no animation clips. Its parts hang off named joints (LowerTorso, UpperTorso, Head,
Left/RightUpperArm, LowerArm, Hand, Left/RightUpperLeg, LowerLeg, Foot), and
Assets/Scripts/Guard/GuardLimbAnimator.cs moves those joints for idle, walk and run.
It is 5.88 units tall as authored; Tools > Guard > Build Guard In Open Scene scales it to 1.85 m.
