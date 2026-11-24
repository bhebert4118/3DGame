Low Poly Prison
===============

139 low-poly prison props in a muted, stylized look, their textures made in code: a wing's block
walls plain, dirty and scratched with tally marks, cell, shower and perimeter walls, cell fronts of
bars, doorways, barred, cell, control room and visiting windows; concrete, epoxy, canteen, shower,
cell and yard floors, an upper floor, a landing's grating, ceilings, a skylight, railings, a net and
stairs; sliding and solid cell doors, the wing key, barred gates, a security door, a card reader, a
keycard and a keypad; the wing's supply, a locking box, a control desk and a monitor bank, a metal
detector and an X-ray scanner, an alarm and its beacon, cameras, intercoms and a visiting phone;
fences, razor wire, a yard gate, a barrier, a watchtower, a searchlight and floodlights; bunks,
steel toilets, the canteen, the laundry, the showers, the officers' post and its riot gear, an
infirmary bed and a library trolley; lights, signs with no real prison service's marks, and a cell's
clutter. 34 of them are machines whose parts move when
they're used. One script runs every machine, the same in Godot and in Unity. One 2048x2048 texture
atlas filtered with mipmaps, the occlusion baked into vertex colours, real-world scale (1 unit = 1
metre).

It is built on Low Poly Industrial Facility's grid, look and textures, as Low Poly Bunker, Low
Poly Sewers and Low Poly Subway are, so the five combine:
https://heyheythere.itch.io/low-poly-industrial-facility
https://heyheythere.itch.io/low-poly-bunker
https://heyheythere.itch.io/low-poly-sewers
https://heyheythere.itch.io/low-poly-subway

The desks have separate colliders for the tabletop equipment, so the wing key and staff card can be picked up. The detector and opened gates leave a standing player passage; the road barrier clears the way when raised.

What's in the zip
-----------------
glb/        one file per prop, with its animations (the best format for Godot, Blender, three.js)
fbx/        one file per prop, with its animations as takes (Unreal, Unity, most 3D apps)
obj/        one file per prop, still (any 3D app)
textures/   atlas.png, the one texture every prop uses, its glow map, and the seamless floor,
            wall and ceiling textures it tiles from, 128x128 a metre, for your own geometry
icons/      an icon per prop, 256x256 PNG with transparency
props.json  every prop: title, group, size, triangles, parts, animations and its "use" (what kind
            of machine it is and how it's set up); "layout" is the demo prison, its "machines"
            (what needs what) and its "puzzle" (the uses that break out of it)
blender/    every prop in one .blend, the atlas packed in; its Level scene is the demo prison
addons/     the Godot addon, low_poly_prison/: this folder is a Godot 4.3+ project that opens on
            its gallery (.gdignore keeps Godot out of the other folders)
unity/      a .unitypackage for Unity 6.3+ (Built-in and URP): prefabs with Machine.cs, the
            pack's lit shader, the demo prison and a demo scene

The machines
------------
A prop that works has a machine on it (machine.gd in Godot, Machine.cs in Unity, the same
script), set up as its "use" in props.json says. What a use does is its kind:
  switch    on and off (the supply, the locking box, the alarm, the intercoms, the visiting phone,
            the washer, the locker, the key cabinet, the infirmary bed)
  slot      takes the item it wants: the swing gate and the solid cell door take the wing key and
            open; the card reader takes the keycard, gives it back and switches on
  keypad    switches on when its code is keyed (the door keypad: 2417; C clears)
  panel     switches on while its switches read its code (the control desk: 1 0 1 1)
  item      picked up into what the player carries (the wing key, the keycard)
  powered   on while what it needs is live (the cell doors, the gates, the security door, the yard
            gate, the barrier, the detector, the X-ray, the beacon, the cameras, the searchlight,
            the monitors, the clocks, the lamps); with nothing to need, a door switches on a use,
            and the lamps are on
A machine is powered while every machine it needs is live, and live while it's on and powered.
It refuses a use without power, without the item it takes or with the wrong code. Its lamps and
screens light while it's live, and it loops its running animation (the beacon, the cameras, the
searchlight, the X-ray's belt, the washer, the clocks).

The grid
--------
Everything snaps to a 2 m grid, 4 m high. A floor or ceiling piece covers one 2 x 2 m cell, its
pivot at the cell's centre on the floor (a ceiling sits 4 m above its pivot, so both go at the
same point). A wall is 2 m long and 0.25 m thick, centred on a cell's edge, its front facing -Y in
Blender (+Z in Godot, -Z in Unity); turn it 90 degrees for the other edges. A post fills the
corner where two walls turn. A cell's walls face into the cell; its front of bars faces the wing,
and a cell is two grid cells deep. The doors and gates go at the same point as their wall and turn
with it: the sliding cell door with the bars round a door, the barred gate with the wide doorway,
the yard gate with the perimeter's gateway. A wall prop (a sign, a lamp, a panel) has its pivot on
the wall's face, at the floor: put it 0.125 m off the wall's line. A ceiling lamp hangs from a
ceiling 4 m above its pivot.

Godot 4.3 and later
-------------------
Copy addons/low_poly_prison/ into your project's addons/ folder. Each prop is a scene in props/:
drag it into your level and turn on grid snapping at 1 m. It has a box collider (StaticBody3D) on
each of its parts, the moving parts' colliders moving with them, and a prop with animations has
an AnimationPlayer. A machine's scene root has machine.gd, set up in the inspector: wire what it
needs there (needs), or in code, and listen for its signals:

    cell_door.needs = [cell_door.get_path_to(locks)]   # locks, cell_door: the props in your level
    locks.switched.connect(func(on): print("cells unlocked ", on))
    locks.use()                                        # what the player's use key calls
    swing_gate.use(carried)                            # takes the wing key out of the carried items

demo/gallery.tscn shows every prop (drag to turn, arrows to browse, A for all of them), and the
prison: L to see it, W to walk it (WASD and the mouse, Shift to run, E uses what you look at, F
for a torch, Esc to stop). demo/gallery.gd is how it wires the prison and uses its machines. The
props use one material, props.tres: the atlas times the vertex colours, lit, with the glow map.

Unity 6.3 and later
-------------------
Import the .unitypackage in unity/. Every prop is a prefab in Assets/LowPolyPrison/Prefabs/ with
box colliders and an Animation component with its clips; a machine's has Machine.cs, set up in
the inspector, with UnityEvents for what it does. Documentation.txt there has the details;
Demo/Level.unity is the prison to walk and break out of and Demo/Demo.unity shows every prop.

Other engines and apps
----------------------
Use fbx/, glb/ or obj/ with textures/atlas.png, filtered, with mipmaps, multiplied by the vertex
colours (the baked occlusion) in your material; textures/atlas_glow.png is what lights itself.
The machines are Godot's and Unity's scripts; elsewhere their animations are there to play, and
props.json says how each is meant to work.

License: see LICENSE.txt. Made by heyheythere: https://heyheythere.itch.io
