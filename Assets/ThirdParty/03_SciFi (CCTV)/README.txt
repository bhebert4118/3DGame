Low Poly Sci-Fi Station Free
============================

16 low-poly props for a space station's control room whose machines work, taken from Low Poly
Sci-Fi Station (106 props, 30 working machines, a station on its emergency power to bring back to
life): https://heyheythere.itch.io/low-poly-sci-fi-station
Built on Low Poly Industrial Facility's grid and look, so the two combine:
https://heyheythere.itch.io/low-poly-industrial-facility
One 2048x2048 texture atlas filtered with mipmaps, the occlusion baked into vertex colours,
real-world scale (1 unit = 1 metre).

What's in the zip
-----------------
glb/        one file per prop, with its animations (the best format for Godot, Blender, three.js)
fbx/        one file per prop, with its animations as takes (Unreal, Unity, most 3D apps)
obj/        one file per prop, still (any 3D app)
textures/   atlas.png, the one texture every prop uses, its glow map, and the seamless wall,
            deck and star textures it tiles from, 128x128 a metre, for your own geometry
icons/      an icon per prop, 256x256 PNG with transparency
props.json  every prop: title, group, size, triangles, parts, animations and its "use" (what kind
            of machine it is and how it's set up)
blender/    every prop in one .blend, the atlas packed in
addons/     the Godot addon, low_poly_sci_fi_station/: this folder is a Godot 4.3+ project that
            opens on its gallery (.gdignore keeps Godot out of the other folders)

The machines
------------
A prop that works has machine.gd on it, set up as its "use" in props.json says: the card reader
takes the keycard, an item to pick up; the console and the main breaker are switches; the door
opens, the wall screen and the server rack light while what they need is live, and with nothing to
need a use switches them; the emergency beacon is on from the start and turns. A machine is
powered while every machine it needs is live, and live while it's on and powered; it refuses a use
without power or without the item it takes.

The grid
--------
The floor is a 2 x 2 m cell, its pivot at its centre, on Low Poly Industrial Facility's 2 m grid.
A wall is 2 m long, 3 m high and 0.25 m thick, centred on a cell's edge, its front facing -Y in
Blender (+Z in Godot): turn it 90 degrees for the other edges, and a post goes where two meet. The
wide window is a 4 m wall, centred on its middle. The door goes at its doorway's point and turn. A
wall prop (the reader, the screen, the beacon) has its pivot on the wall's face, at the floor: put
it 0.125 m off the wall's line. The starfield is a shell 80 m across, seen from inside.

Godot 4.3 and later
-------------------
Copy addons/low_poly_sci_fi_station/ into your project's addons/ folder. Each prop is a scene in
props/: drag it into your level and turn on grid snapping at 1 m. It has a box collider
(StaticBody3D) on each of its parts, the moving parts' colliders moving with them, and a prop with
animations has an AnimationPlayer. Wire what a machine needs in the inspector (needs), or in code,
and listen for its signals:

    door.needs = [door.get_path_to(reader)]   # reader, door: in your level
    reader.use(carried)                       # takes the keycard out of the carried items

demo/gallery.tscn shows every prop: drag to turn, arrows to browse, A for all of them.
Low Poly Sci-Fi Station uses the same addon folder: it installs over this one.
The props use one material, props.tres: the atlas times the vertex colours, lit, with the glow map.

Other engines and apps
----------------------
Use fbx/, glb/ or obj/ with textures/atlas.png, filtered, with mipmaps, multiplied by the vertex
colours (the baked occlusion) in your material. The machines are Godot's script; elsewhere their
animations are there to play, and props.json says how each is meant to work.

The station is made up: no agency's, maker's or ship's name or emblem is shown.

License: CC BY 4.0, see LICENSE.txt for the credit line. Made by heyheythere:
https://heyheythere.itch.io
