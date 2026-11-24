Low Poly Bunker Free
====================

16 low-poly bunker props for a guard post whose machines work, taken from Low Poly Bunker (150
props, 38 working machines, a bunker to power up):
https://heyheythere.itch.io/low-poly-bunker
Built on Low Poly Industrial Facility's grid and look, so the two combine:
https://heyheythere.itch.io/low-poly-industrial-facility
One 2048x2048 texture atlas filtered with mipmaps, the occlusion baked into vertex colours,
real-world scale (1 unit = 1 metre).

What's in the zip
-----------------
glb/        one file per prop, with its animations (the best format for Godot, Blender, three.js)
fbx/        one file per prop, with its animations as takes (Unreal, Unity, most 3D apps)
obj/        one file per prop, still (any 3D app)
textures/   atlas.png, the one texture every prop uses, its glow map, and the seamless floor,
            wall and ceiling textures it tiles from, 128x128 a metre, for your own geometry
icons/      an icon per prop, 256x256 PNG with transparency
props.json  every prop: title, group, size, triangles, parts, animations and its "use" (what kind
            of machine it is and how it's set up)
blender/    every prop in one .blend, the atlas packed in
addons/     the Godot addon, low_poly_bunker/: this folder is a Godot 4.3+ project that opens on
            its gallery (.gdignore keeps Godot out of the other folders)
unity/      a .unitypackage for Unity 6.3+ (Built-in and URP): prefabs with Machine.cs, the
            pack's lit shader and a demo scene

The machines
------------
A prop that works has a machine on it (machine.gd in Godot, Machine.cs in Unity, the same script),
set up as its "use" in props.json says: the breaker switches on and off, and the door, the cage
lamp, the beacon and the emergency lamp are on while what they need is live (the lamps are on by
themselves, needing nothing; with nothing to need, the door opens and shuts on a use). A machine
is powered while every machine it needs is live, and live while it's on and powered; it refuses a
use without power.

The grid
--------
Everything snaps to a 2 m grid, 4 m high. A floor or ceiling piece covers one 2 x 2 m cell, its
pivot at the cell's centre on the floor (a ceiling sits 4 m above its pivot, so both go at the
same point). A wall is 2 m long and 0.25 m thick, centred on a cell's edge, its front facing -Y
in Blender (+Z in Godot, -Z in Unity); turn it 90 degrees for the other edges. A post fills the
corner where two walls turn. The door goes at the same point as its doorway and turns with it. A
wall prop (the breaker, the lamps, the beacon, the sign) has its pivot on the wall's face, at the
floor: put it 0.125 m off the wall's line.

Godot 4.3 and later
-------------------
Copy addons/low_poly_bunker/ into your project's addons/ folder. Each prop is a scene in props/:
drag it into your level and turn on grid snapping at 1 m. It has a box collider (StaticBody3D) on
each of its parts, the door's moving with it, and a prop with animations has an AnimationPlayer.
Wire what a machine needs in the inspector (needs), or in code, and listen for its signals:

    door.needs = [door.get_path_to(breaker)]   # breaker, door: the props in your level
    breaker.switched.connect(func(on): print("breaker ", on))
    breaker.use()                              # what the player's use key calls

demo/gallery.tscn shows every prop: drag to turn, arrows to browse, A for all of them.
Low Poly Bunker uses the same addon folder: it installs over this one.
The props use one material, props.tres: the atlas times the vertex colours, lit, with the glow map.

Unity 6.3 and later
-------------------
Import the .unitypackage in unity/. Every prop is a prefab with box colliders and an Animation
component with its clips; a machine's has Machine.cs, set up in the inspector, with UnityEvents
for what it does. Documentation.txt there has the details.

Other engines and apps
----------------------
Use fbx/, glb/ or obj/ with textures/atlas.png, filtered, with mipmaps, multiplied by the vertex
colours (the baked occlusion) in your material. The machines are Godot's and Unity's scripts;
elsewhere their animations are there to play, and props.json says how each is meant to work.

License: CC BY 4.0, see LICENSE.txt for the credit line. Made by heyheythere:
https://heyheythere.itch.io
