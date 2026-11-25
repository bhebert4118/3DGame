using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tools > Guard > Build Guard In Open Scene
///
/// Sets up the guards, and the parts of the game flow that hang off them, in PrisonLevel in one click.
/// Everything it creates lives under one root object called "Guards". It does not change the level,
/// puzzle or door objects other people own; its own objects only point at them:
///   - the patrol guard ("Guard"): waypoints through the hallway and cell block lobby, and a
///     distraction route ending at Guard_Alarm_Distract_Destination
///   - the room guard ("Guard_Room"): stands at a post in the guard room watching the monitor wall
///     (the alarm computer is one of the monitors), walks over to thrown rubbish, and has his own
///     route out to the distraction room
///   - each guard's model from Assets/ThirdParty/05_Guard (scaled to 1.85 m), GuardLimbAnimator,
///     GuardVision, and a CapsuleCollider + kinematic Rigidbody on the guard root
///   - two GuardDoor objects standing in the distraction-room and guard-room doorways. Each points at
///     the existing blocker collider and visible door there, so the door swings open for the guards
///   - GuardAlarmLink, which sends both guards off for 15 seconds whenever the alarm computer raises onSolved
///   - a click target over the guard-room bin that hands out rubbish to throw
///   - a CaughtRestart object that restarts the level when a guard catches the player
/// The one thing it adds outside "Guards" is on the Player object: GuardDetectable + PlayerSuspicion
/// (so he can be caught) and PlayerThrow (so he can throw).
///
/// Safe to run again: it reuses what is already there, and it moves anything an earlier version of
/// this tool attached to other people's objects back under "Guards".
/// If the scene ever conflicts in git, take the other version and just run this again.
/// </summary>
public static class GuardSceneBuilder
{
    private const string RootName = "Guards";
    private const string WaypointHolderName = "Waypoints";
    private const string DoorHolderName = "Doors";
    private const string PatrolGuardName = "Guard";
    private const string RoomGuardName = "Guard_Room";
    private const string RouteRootName = "DistractionRoute";
    private const string PostName = "Post_GuardRoom";
    private const string DestinationName = "Guard_Alarm_Distract_Destination";
    private const string DistractDoorName = "guard_distract_room_door";       // invisible blocker in the doorway
    private const string DistractDoorVisualName = "Real_GuardDistract_Door";  // the door model you can see
    private const string GuardRoomDoorName = "Placeholder_GuardDoor";
    private const string GuardRoomDoorVisualName = "Real_GuardRoom_Door";
    private const string BodyMatPath = "Assets/Materials/Guard_Placeholder_MAT.mat";
    private const string VisorMatPath = "Assets/Materials/Guard_Visor_Placeholder_MAT.mat";
    private const string ModelPath = "Assets/ThirdParty/05_Guard/low-poly-prison-guard.glb";
    private const string ModelHolderName = "Model";
    private const float GuardHeight = 1.85f;  // metres, cap included (the doors are 2.2 m)
    private const float GuardRadius = 0.35f;  // metres, about the width of the torso
    private const float AlarmSeconds = 15f;   // how long the alarm keeps a guard in the distraction room
    private static readonly string[] PlaceholderParts = { "Body_Placeholder", "Visor_Placeholder" };

    // Holders in the level that earlier versions of this tool put things under. They are left empty again.
    private const string LegacyWaypointRootName = "GuardWaypoints";

    // World positions picked from the greybox layout (hallway x -2.5..2.5, lobby z -26..-22).
    private static readonly (string name, Vector3 pos)[] PatrolPoints =
    {
        ("WP_Hallway_North", new Vector3(0f, 0f, -10f)),
        ("WP_Lobby_Center",  new Vector3(0f, 0f, -24f)),
        ("WP_Lobby_West",    new Vector3(-3.5f, 0f, -24f)),  // in front of the target cell
        ("WP_Lobby_East",    new Vector3(3.5f, 0f, -24f)),   // in front of the decoy cell
    };
    // Patrol order (indices into PatrolPoints). Center appears twice so the guard never cuts through a wall corner.
    private static readonly int[] PatrolOrder = { 0, 1, 2, 3, 1 };

    // The room guard stands a little behind the desk, to the player's right, watching the monitor wall.
    // The alarm computer is one of those monitors, so anyone working at it is in front of him.
    private static readonly Vector3 RoomPostPosition = new Vector3(0.9f, 0f, -3.2f);
    private const float RoomPostYaw = -12f;
    // A guard on watch: slow, slight sways and unhurried turns, with a wide field of view on the screens.
    private const float RoomGuardLookAround = 6f;     // degrees either side
    private const float RoomGuardLookRate = 0.12f;    // sways per second (about one every eight seconds)
    private const float RoomGuardTurnSpeed = 60f;     // degrees per second while standing
    private const float RoomGuardViewAngle = 110f;    // degrees; covers every spot the alarm computer can be used from
    // Where earlier versions of this tool put him. A post still sitting exactly here gets moved to the new spot.
    private static readonly Vector3 OldRoomPostPosition = new Vector3(2.6f, 0f, -1.3f);
    private const float OldRoomGuardLookAround = 15f;
    private const float DefaultViewAngle = 80f;
    // His way out when the alarm sounds: guard-room door, hallway, lobby, distraction-room door, his own spot inside.
    private static readonly Vector3 RoomExitPoint = new Vector3(0f, 0f, -6.8f);
    private static readonly Vector3 DistractDoorwayPoint = new Vector3(7.6f, 0f, -23.95f);
    private static readonly Vector3 RoomGuardWaitPoint = new Vector3(9.6f, 0f, -22.7f);

    [MenuItem("Tools/Guard/Build Guard In Open Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Build Guard");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject destination = GameObject.Find(DestinationName);
        if (destination == null)
        {
            EditorUtility.DisplayDialog("Guard Builder",
                $"Couldn't find '{DestinationName}' in the open scene.\n\nOpen Assets/Scenes/PrisonLevel first.", "OK");
            return;
        }

        GameObject rootObject = GameObject.Find(RootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Guards root");
        }
        Transform root = rootObject.transform;

        // --- Waypoints and routes --------------------------------------------------
        Transform waypoints = FindOrCreateChild(root, WaypointHolderName, Vector3.zero);
        ReclaimWaypoints(waypoints);

        var points = new Transform[PatrolPoints.Length];
        for (int i = 0; i < PatrolPoints.Length; i++)
            points[i] = FindOrCreateChild(waypoints, PatrolPoints[i].name, PatrolPoints[i].pos);

        var patrol = new List<Transform>();
        foreach (int i in PatrolOrder) patrol.Add(points[i]);

        Transform routeRoot = FindOrCreateChild(waypoints, RouteRootName, Vector3.zero);
        Transform lobbyCentre = FindOrCreateChild(routeRoot, "Route_LobbyCenter", PatrolPoints[1].pos);
        var patrolRoute = new List<Transform> { lobbyCentre, destination.transform };

        Transform post = FindOrCreateChild(waypoints, PostName, RoomPostPosition, RoomPostYaw);
        bool postMoved = (post.position - OldRoomPostPosition).sqrMagnitude < 0.01f;
        if (postMoved)
        {
            Undo.RecordObject(post, "Move guard-room post");
            post.SetPositionAndRotation(RoomPostPosition, Quaternion.Euler(0f, RoomPostYaw, 0f));
        }
        var roomRoute = new List<Transform>
        {
            FindOrCreateChild(routeRoot, "Route_GuardRoomExit", RoomExitPoint),
            points[0],
            lobbyCentre,
            FindOrCreateChild(routeRoot, "Route_DistractDoorway", DistractDoorwayPoint),
            FindOrCreateChild(routeRoot, "Wait_RoomGuard", RoomGuardWaitPoint),
        };

        // --- Doors -----------------------------------------------------------------
        Transform doors = FindOrCreateChild(root, DoorHolderName, Vector3.zero);
        GuardDoor distractDoor = EnsureDoor(doors, "Door_DistractionRoom", DistractDoorName, DistractDoorVisualName, out string distractDoorStatus);
        GuardDoor guardRoomDoor = EnsureDoor(doors, "Door_GuardRoom", GuardRoomDoorName, GuardRoomDoorVisualName, out string guardRoomDoorStatus);

        // --- Guards ----------------------------------------------------------------
        GuardController patrolGuard = FindGuard(PatrolGuardName);
        if (patrolGuard == null) patrolGuard = CreatePlaceholderGuard(PatrolGuardName, points[0].position, 0f);
        Configure(patrolGuard, patrol, patrolRoute, new List<GuardDoor> { distractDoor }, atPost: false);

        GuardController roomGuard = FindGuard(RoomGuardName);
        bool roomGuardIsNew = roomGuard == null;
        if (roomGuardIsNew) roomGuard = CreatePlaceholderGuard(RoomGuardName, post.position, post.eulerAngles.y);
        else if (postMoved)
        {
            Undo.RecordObject(roomGuard.transform, "Move room guard to his post");
            roomGuard.transform.SetPositionAndRotation(post.position, post.rotation);
        }
        Configure(roomGuard, new List<Transform> { post }, roomRoute, new List<GuardDoor> { guardRoomDoor, distractDoor }, atPost: true);

        string modelStatus = "";
        foreach (GuardController guard in new[] { patrolGuard, roomGuard })
        {
            KeepUnder(root, guard.transform);
            if (guard.GetComponent<GuardVision>() == null) Undo.AddComponent<GuardVision>(guard.gameObject);
            modelStatus = AttachModel(guard);
            EnsurePhysics(guard);
        }
        ApplyWatchSettings(roomGuard, roomGuardIsNew);

        // --- Game flow pieces ------------------------------------------------------
        string alarmStatus = ConnectAlarm(rootObject);
        string playerStatus = EnsurePlayer();
        string trashStatus = EnsureTrashCan(root);
        EnsureCaughtRestart(root);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = roomGuard.gameObject;

        Debug.Log("[Guard Builder] Done. Patrol guard: " + patrol.Count + " stops. Room guard: post + " + roomRoute.Count
            + " route points" + (roomGuardIsNew ? " (new)" : "") + ". " + alarmStatus + " " + modelStatus
            + " Distraction door: " + distractDoorStatus + " Guard-room door: " + guardRoomDoorStatus
            + " " + playerStatus + " " + trashStatus, roomGuard);
    }

    // ------------------------------------------------------------------ guards

    private static GuardController FindGuard(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<GuardController>() : null;
    }

    /// <summary>
    /// Writes a guard's patrol, alarm route and doors. <paramref name="atPost"/> is for a guard who
    /// stands watch at one spot: he faces the way his post points and walks over to noises.
    /// </summary>
    private static void Configure(GuardController guard, List<Transform> patrol, List<Transform> route,
        List<GuardDoor> doors, bool atPost)
    {
        doors.RemoveAll(d => d == null);

        var so = new SerializedObject(guard);
        SetList(so.FindProperty("patrolWaypoints"), patrol);
        SetList(so.FindProperty("distractionRoute"), route);
        SetList(so.FindProperty("routeDoors"), doors);
        so.FindProperty("distractionDuration").floatValue = AlarmSeconds;
        so.FindProperty("faceWaypointDirection").boolValue = atPost;
        so.FindProperty("reactsToNoise").boolValue = atPost;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Makes the room guard behave like a guard on watch: slow, slight sways, unhurried turns and a
    /// wide view of the screens. Written for a guard this run created, or one still on the values an
    /// earlier version of this tool gave him, so numbers tuned in the Inspector are left alone.
    /// </summary>
    private static void ApplyWatchSettings(GuardController guard, bool isNew)
    {
        var so = new SerializedObject(guard);
        SerializedProperty lookAround = so.FindProperty("lookAroundAngle");
        if (isNew || Mathf.Approximately(lookAround.floatValue, OldRoomGuardLookAround))
        {
            lookAround.floatValue = RoomGuardLookAround;
            so.FindProperty("lookAroundRate").floatValue = RoomGuardLookRate;
            so.FindProperty("standingTurnSpeed").floatValue = RoomGuardTurnSpeed;
            so.ApplyModifiedProperties();
        }

        var vision = guard.GetComponent<GuardVision>();
        if (vision == null) return;
        var visionSo = new SerializedObject(vision);
        SerializedProperty viewAngle = visionSo.FindProperty("viewAngle");
        if (isNew || Mathf.Approximately(viewAngle.floatValue, DefaultViewAngle))
        {
            viewAngle.floatValue = RoomGuardViewAngle;
            visionSo.ApplyModifiedProperties();
        }
    }

    private static GuardController CreatePlaceholderGuard(string name, Vector3 position, float yaw)
    {
        var root = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(root, "Create " + name);
        root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        // Body: capsule, feet at the root's origin. Looks only; the guard's collider goes on the root (EnsurePhysics).
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = PlaceholderParts[0];
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        body.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(BodyMatPath, new Color(0.15f, 0.2f, 0.35f));

        // Visor shows which way he's facing.
        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = PlaceholderParts[1];
        Object.DestroyImmediate(visor.GetComponent<Collider>());
        visor.transform.SetParent(root.transform, false);
        visor.transform.localPosition = new Vector3(0f, 1.6f, 0.33f);
        visor.transform.localScale = new Vector3(0.45f, 0.12f, 0.15f);
        visor.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(VisorMatPath, new Color(1f, 0.8f, 0.1f));

        return root.AddComponent<GuardController>();
    }

    /// <summary>
    /// Gives the guard one body for physics: a capsule on the root, with a kinematic Rigidbody because
    /// he is moved by script. The player then bumps into him instead of walking through, and
    /// GuardController can sweep the capsule ahead to stop for anyone in the way.
    /// </summary>
    private static void EnsurePhysics(GuardController guard)
    {
        GameObject go = guard.gameObject;

        if (go.GetComponent<CapsuleCollider>() == null)
        {
            var capsule = Undo.AddComponent<CapsuleCollider>(go);
            capsule.direction = 1; // upright (Y axis)
            capsule.height = GuardHeight;
            capsule.radius = GuardRadius;
            capsule.center = new Vector3(0f, GuardHeight * 0.5f, 0f);
        }

        var body = go.GetComponent<Rigidbody>();
        if (body == null) body = Undo.AddComponent<Rigidbody>(go);
        Undo.RecordObject(body, "Set up guard body");
        body.isKinematic = true;
        body.useGravity = false;

        // Earlier versions of this tool left the collider on the placeholder capsule; the root has it now.
        Transform placeholder = go.transform.Find(PlaceholderParts[0]);
        Collider old = placeholder != null ? placeholder.GetComponent<Collider>() : null;
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    /// <summary>
    /// Puts the real guard model under the guard, facing the way the guard faces, scaled to
    /// <see cref="GuardHeight"/> with its feet on the floor, and hides the placeholder capsule.
    /// Returns a line for the log. Does nothing if the model is already there or hasn't been imported.
    /// </summary>
    private static string AttachModel(GuardController guard)
    {
        Transform root = guard.transform;
        if (root.Find(ModelHolderName) != null) return "Models attached.";

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null)
            return $"No model at {ModelPath} (is the glTFast package installed?) - using the placeholder.";

        var holder = new GameObject(ModelHolderName);
        Undo.RegisterCreatedObjectUndo(holder, "Attach guard model");
        holder.transform.SetParent(root, false);

        var model = PrefabUtility.InstantiatePrefab(asset, holder.transform) as GameObject;
        if (model == null) model = Object.Instantiate(asset, holder.transform);

        // Turn the model to face the guard's forward. Its own forward is worked out from the hips,
        // so this holds whichever way the importer happens to orient the file.
        Transform leftHip = GuardLimbAnimator.FindJoint(model.transform, "LeftUpperLeg");
        Transform rightHip = GuardLimbAnimator.FindJoint(model.transform, "RightUpperLeg");
        if (leftHip != null && rightHip != null)
        {
            Vector3 right = Vector3.ProjectOnPlane(rightHip.position - leftHip.position, Vector3.up);
            if (right.sqrMagnitude > 1e-6f)
            {
                Vector3 modelForward = Vector3.Cross(right.normalized, Vector3.up);
                float yaw = Vector3.SignedAngle(modelForward, root.forward, Vector3.up);
                holder.transform.Rotate(Vector3.up, yaw, Space.World);
            }
        }

        // The file is authored about 5.9 units tall; bring it to a person's height, feet on the floor.
        if (TryGetBounds(holder, out Bounds bounds) && bounds.size.y > 0.001f)
        {
            holder.transform.localScale *= GuardHeight / bounds.size.y;
            if (TryGetBounds(holder, out bounds))
                holder.transform.position += Vector3.up * (root.position.y - bounds.min.y);
        }

        foreach (string partName in PlaceholderParts)
        {
            Transform part = root.Find(partName);
            Renderer partRenderer = part != null ? part.GetComponent<Renderer>() : null;
            if (partRenderer == null) continue;
            Undo.RecordObject(partRenderer, "Hide guard placeholder");
            partRenderer.enabled = false;
        }

        if (guard.GetComponent<GuardLimbAnimator>() == null) Undo.AddComponent<GuardLimbAnimator>(guard.gameObject);
        return "Models attached.";
    }

    // ------------------------------------------------------------------ doors

    /// <summary>
    /// Makes sure there is a GuardDoor object of ours standing in a doorway. It points at the existing
    /// blocker collider there and at the visible door's Animation, and switches those at run time;
    /// neither of those objects is changed in the scene. An earlier version of this tool put the
    /// GuardDoor on the blocker itself, so that one is taken off again.
    /// </summary>
    private static GuardDoor EnsureDoor(Transform holder, string doorName, string blockerName, string visualName, out string status)
    {
        Transform mine = holder.Find(doorName);
        GameObject blocker = GameObject.Find(blockerName);
        if (blocker == null)
        {
            status = $"'{blockerName}' not found, guards will walk straight through.";
            return mine != null ? mine.GetComponent<GuardDoor>() : null;
        }

        var onBlocker = blocker.GetComponent<GuardDoor>();
        if (onBlocker != null) Undo.DestroyObjectImmediate(onBlocker);

        if (mine == null)
        {
            var go = new GameObject(doorName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + doorName);
            go.transform.SetParent(holder, false);
            mine = go.transform;
        }
        // It stands in the doorway, because guards open a door by how close they are to this object.
        mine.SetPositionAndRotation(blocker.transform.position, blocker.transform.rotation);

        var door = mine.GetComponent<GuardDoor>();
        if (door == null) door = Undo.AddComponent<GuardDoor>(mine.gameObject);

        var so = new SerializedObject(door);
        SerializedProperty animationProp = so.FindProperty("doorAnimation");
        so.FindProperty("blocker").objectReferenceValue = blocker.GetComponent<Collider>();
        if (animationProp.objectReferenceValue == null)
            animationProp.objectReferenceValue = FindDoorAnimation(visualName, blocker.transform.position);
        so.ApplyModifiedProperties();

        status = animationProp.objectReferenceValue != null
            ? "connected to its model."
            : $"no model to animate (looked for '{visualName}'), it will only stop blocking.";
        return door;
    }

    private static Animation FindDoorAnimation(string visualName, Vector3 doorway)
    {
        GameObject named = GameObject.Find(visualName);
        Animation fromName = named != null ? named.GetComponentInChildren<Animation>(true) : null;
        if (fromName != null) return fromName;

        // Renamed? Take the nearest animated model standing in the same doorway.
        Animation best = null;
        float bestDistance = 1f;
        foreach (Animation candidate in Object.FindObjectsByType<Animation>(FindObjectsInactive.Include))
        {
            Vector3 offset = candidate.transform.position - doorway;
            offset.y = 0f;
            if (offset.magnitude >= bestDistance) continue;
            bestDistance = offset.magnitude;
            best = candidate;
        }
        return best;
    }

    // ------------------------------------------------------------------ game flow

    /// <summary>
    /// The alarm sends every guard to the distraction room. That is done by a GuardAlarmLink on our
    /// own root, which listens to the alarm computer while the game runs. An earlier version of this
    /// tool instead added the guards to the alarm computer's own onSolved list; those entries are removed.
    /// </summary>
    private static string ConnectAlarm(GameObject root)
    {
        var alarm = Object.FindFirstObjectByType<AlarmComputerInteractable>(FindObjectsInactive.Include);
        if (alarm != null && alarm.onSolved != null)
        {
            for (int i = alarm.onSolved.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (!(alarm.onSolved.GetPersistentTarget(i) is GuardController)) continue;
                if (alarm.onSolved.GetPersistentMethodName(i) != nameof(GuardController.Distract)) continue;
                Undo.RecordObject(alarm, "Take guards off the alarm computer's event list");
                UnityEventTools.RemovePersistentListener(alarm.onSolved, i);
                EditorUtility.SetDirty(alarm);
            }
        }

        if (root.GetComponent<GuardAlarmLink>() == null) Undo.AddComponent<GuardAlarmLink>(root);
        return alarm != null
            ? "Guards listen for the alarm."
            : "No AlarmComputerInteractable in the scene yet; the guards will listen for one when the game runs.";
    }

    /// <summary>Lets guards catch the player (GuardDetectable + PlayerSuspicion) and lets the player throw rubbish.</summary>
    private static string EnsurePlayer()
    {
        var player = Object.FindFirstObjectByType<FirstPersonPlayerController>(FindObjectsInactive.Include);
        if (player == null) return "No player found, so nobody can be caught or throw.";

        GameObject go = player.gameObject;
        if (go.GetComponent<GuardDetectable>() == null) Undo.AddComponent<GuardDetectable>(go);
        if (go.GetComponent<PlayerSuspicion>() == null) Undo.AddComponent<PlayerSuspicion>(go);
        if (go.GetComponent<PlayerThrow>() == null) Undo.AddComponent<PlayerThrow>(go);
        return "Player can be caught and can throw.";
    }

    /// <summary>
    /// Adds an invisible click target over the guard-room bin. It is a little bigger than the prop,
    /// so the player's look ray reaches it before the bin itself. The bin prop is only measured.
    /// </summary>
    private static string EnsureTrashCan(Transform root)
    {
        var existing = Object.FindFirstObjectByType<TrashCanInteractable>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Transform existingHolder = existing.transform.parent != null ? existing.transform.parent : existing.transform;
            KeepUnder(root, existingHolder);
            return "Trash can already set up.";
        }

        GameObject set = GameObject.Find("GuardRoom_Set");
        Transform bin = set != null ? GuardLimbAnimator.FindJoint(set.transform, "Bin") : null;
        Bounds bounds = new Bounds(new Vector3(-2.15f, 0.4f, -0.94f), new Vector3(0.4f, 0.8f, 0.45f)); // its usual spot
        Bounds measured = default;
        bool foundBin = bin != null && TryGetBounds(bin.gameObject, out measured);
        if (foundBin) bounds = measured;

        // Its own empty parent, so the hover outline is drawn round the bin only.
        var holder = new GameObject("TrashCan");
        Undo.RegisterCreatedObjectUndo(holder, "Create trash can click target");
        holder.transform.SetParent(root, true);
        holder.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        var target = new GameObject("TrashCan_Interactable");
        target.transform.SetParent(holder.transform, false);
        target.transform.localPosition = new Vector3(0f, bounds.size.y * 0.5f, 0f);
        var box = target.AddComponent<BoxCollider>();
        box.size = bounds.size + Vector3.one * 0.12f;
        target.AddComponent<TrashCanInteractable>();

        return foundBin ? "Trash can click target placed on the bin." : "Bin prop not found, trash can click target placed at its usual spot.";
    }

    private static void EnsureCaughtRestart(Transform root)
    {
        var existing = Object.FindFirstObjectByType<CaughtRestart>(FindObjectsInactive.Include);
        if (existing != null)
        {
            KeepUnder(root, existing.transform);
            return;
        }
        var go = new GameObject("CaughtRestart");
        Undo.RegisterCreatedObjectUndo(go, "Create CaughtRestart");
        go.transform.SetParent(root, false);
        go.AddComponent<CaughtRestart>();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Earlier versions of this tool parented the waypoints under the level's own GuardWaypoints
    /// holder. Move them under ours, which leaves that holder exactly as it was.
    /// </summary>
    private static void ReclaimWaypoints(Transform waypoints)
    {
        GameObject legacy = GameObject.Find(LegacyWaypointRootName);
        if (legacy == null) return;

        var names = new List<string> { RouteRootName, PostName };
        foreach (var point in PatrolPoints) names.Add(point.name);
        foreach (string name in names)
        {
            Transform child = legacy.transform.Find(name);
            if (child != null && waypoints.Find(name) == null) Undo.SetTransformParent(child, waypoints, "Move guard waypoint");
        }
    }

    /// <summary>Parents one of our objects under the Guards root, keeping it where it stands.</summary>
    private static void KeepUnder(Transform root, Transform ours)
    {
        if (ours.parent != root) Undo.SetTransformParent(ours, root, "Move under Guards");
    }

    private static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    private static Transform FindOrCreateChild(Transform parent, string name, Vector3 worldPos, float yaw = 0f)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
        return go.transform;
    }

    private static void SetList<T>(SerializedProperty prop, List<T> items) where T : Object
    {
        prop.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static Material GetOrCreateMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }
}
