#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces the ragdoll wizard Unity removed in Unity 6.
///
/// Usage:
///   1. Open your NPC prefab (double-click it in the Project window).
///   2. Expand its skeleton in the Hierarchy panel until you can see individual bones.
///   3. Open this window via GTI > Setup NPC Ragdoll in the top menu bar.
///   4. Drag each bone from the Hierarchy into the matching slot.
///   5. Click Build Ragdoll.
///   6. Add the NPCRagdoll script to the NPC root and drag the Pelvis/Hips bone
///      into its Root Bone field.
///
/// The tool adds Rigidbody, CapsuleCollider, and CharacterJoint to each bone.
/// All Rigidbodies start kinematic and all CapsuleColliders start disabled.
/// NPCRagdoll toggles both at runtime when a ragdoll is triggered.
/// Running the tool again on a prefab that already has ragdoll components
/// is safe — it removes the old ones first.
/// </summary>
public class RagdollCreatorWindow : EditorWindow
{
    private Transform _pelvis;
    private Transform _spine;
    private Transform _head;
    private Transform _leftUpperLeg, _leftLowerLeg, _leftFoot;
    private Transform _rightUpperLeg, _rightLowerLeg, _rightFoot;
    private Transform _leftUpperArm, _leftForearm;
    private Transform _rightUpperArm, _rightForearm;

    private float _totalMass = 20f;

    [MenuItem("GTI/Setup NPC Ragdoll")]
    public static void Open() => GetWindow<RagdollCreatorWindow>("Ragdoll Setup");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Open the NPC prefab and expand its skeleton in the Hierarchy. " +
            "Drag each bone from the Hierarchy into the matching slot below. " +
            "Synty bone names are listed in brackets as a guide.",
            MessageType.Info);

        GUILayout.Space(8);
        GUILayout.Label("Core", EditorStyles.boldLabel);
        _pelvis = Slot("Pelvis / Hips  [Hips]",  _pelvis);
        _spine  = Slot("Spine  [Spine1 or Spine]", _spine);
        _head   = Slot("Head  [Head]",             _head);

        GUILayout.Space(8);
        GUILayout.Label("Left Leg", EditorStyles.boldLabel);
        _leftUpperLeg = Slot("Upper Leg  [LeftUpLeg]",  _leftUpperLeg);
        _leftLowerLeg = Slot("Lower Leg  [LeftLeg]",    _leftLowerLeg);
        _leftFoot     = Slot("Foot  [LeftFoot]",        _leftFoot);

        GUILayout.Space(8);
        GUILayout.Label("Right Leg", EditorStyles.boldLabel);
        _rightUpperLeg = Slot("Upper Leg  [RightUpLeg]", _rightUpperLeg);
        _rightLowerLeg = Slot("Lower Leg  [RightLeg]",   _rightLowerLeg);
        _rightFoot     = Slot("Foot  [RightFoot]",       _rightFoot);

        GUILayout.Space(8);
        GUILayout.Label("Left Arm", EditorStyles.boldLabel);
        _leftUpperArm = Slot("Upper Arm  [LeftArm]",      _leftUpperArm);
        _leftForearm  = Slot("Forearm  [LeftForeArm]",    _leftForearm);

        GUILayout.Space(8);
        GUILayout.Label("Right Arm", EditorStyles.boldLabel);
        _rightUpperArm = Slot("Upper Arm  [RightArm]",    _rightUpperArm);
        _rightForearm  = Slot("Forearm  [RightForeArm]",  _rightForearm);

        GUILayout.Space(10);
        _totalMass = EditorGUILayout.FloatField("Total Mass", _totalMass);

        GUILayout.Space(10);

        bool ready = _pelvis && _spine && _head &&
                     _leftUpperLeg  && _leftLowerLeg  && _leftFoot &&
                     _rightUpperLeg && _rightLowerLeg && _rightFoot &&
                     _leftUpperArm  && _leftForearm &&
                     _rightUpperArm && _rightForearm;

        GUI.enabled = ready;
        if (GUILayout.Button("Build Ragdoll", GUILayout.Height(34)))
            Build();
        GUI.enabled = true;

        if (!ready)
            EditorGUILayout.HelpBox("Fill in all slots to enable Build.", MessageType.None);
    }

    private static Transform Slot(string label, Transform current) =>
        (Transform)EditorGUILayout.ObjectField(label, current, typeof(Transform), true);

    private void Build()
    {
        Undo.SetCurrentGroupName("Build Ragdoll");
        int undoGroup = Undo.GetCurrentGroup();

        float m = _totalMass;

        // Pelvis is the anchor of the whole ragdoll — no CharacterJoint, just a body.
        // direction 1 = Y axis capsule (vertical, fits the torso/hip block).
        AddBone(_pelvis, connectedTo: null, mass: m * 0.25f, radius: 0.10f, height: 0.20f, capsuleDir: 1);

        AddBone(_spine, connectedTo: _pelvis,
            mass: m * 0.15f, radius: 0.10f, height: 0.20f, capsuleDir: 1,
            lowTwist: -30, highTwist: 30, swing: 30);

        AddBone(_head, connectedTo: _spine,
            mass: m * 0.08f, radius: 0.08f, height: 0.15f, capsuleDir: 1,
            lowTwist: -30, highTwist: 30, swing: 20);

        // Legs — direction 1 = Y axis capsule (runs along the leg length).
        // Lower legs only bend forward (knee), so highTwist is capped.
        AddBone(_leftUpperLeg, connectedTo: _pelvis,
            mass: m * 0.10f, radius: 0.07f, height: 0.22f, capsuleDir: 1,
            lowTwist: -60, highTwist: 30, swing: 30);

        AddBone(_leftLowerLeg, connectedTo: _leftUpperLeg,
            mass: m * 0.08f, radius: 0.06f, height: 0.20f, capsuleDir: 1,
            lowTwist: 0, highTwist: 80, swing: 5);

        AddBone(_leftFoot, connectedTo: _leftLowerLeg,
            mass: m * 0.03f, radius: 0.04f, height: 0.10f, capsuleDir: 0,
            lowTwist: -30, highTwist: 30, swing: 15);

        AddBone(_rightUpperLeg, connectedTo: _pelvis,
            mass: m * 0.10f, radius: 0.07f, height: 0.22f, capsuleDir: 1,
            lowTwist: -60, highTwist: 30, swing: 30);

        AddBone(_rightLowerLeg, connectedTo: _rightUpperLeg,
            mass: m * 0.08f, radius: 0.06f, height: 0.20f, capsuleDir: 1,
            lowTwist: 0, highTwist: 80, swing: 5);

        AddBone(_rightFoot, connectedTo: _rightLowerLeg,
            mass: m * 0.03f, radius: 0.04f, height: 0.10f, capsuleDir: 0,
            lowTwist: -30, highTwist: 30, swing: 15);

        // Arms — direction 0 = X axis capsule (runs along the arm length).
        // Forearms only bend one way (elbow), so lowTwist is 0.
        AddBone(_leftUpperArm, connectedTo: _spine,
            mass: m * 0.05f, radius: 0.05f, height: 0.22f, capsuleDir: 0,
            lowTwist: -70, highTwist: 70, swing: 50);

        AddBone(_leftForearm, connectedTo: _leftUpperArm,
            mass: m * 0.04f, radius: 0.04f, height: 0.18f, capsuleDir: 0,
            lowTwist: 0, highTwist: 90, swing: 15);

        AddBone(_rightUpperArm, connectedTo: _spine,
            mass: m * 0.05f, radius: 0.05f, height: 0.22f, capsuleDir: 0,
            lowTwist: -70, highTwist: 70, swing: 50);

        AddBone(_rightForearm, connectedTo: _rightUpperArm,
            mass: m * 0.04f, radius: 0.04f, height: 0.18f, capsuleDir: 0,
            lowTwist: 0, highTwist: 90, swing: 15);

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("[GTI] Ragdoll built. Now add NPCRagdoll to the NPC root and set Root Bone to the Pelvis/Hips bone.");
    }

    private static void AddBone(
        Transform bone,
        Transform connectedTo,
        float mass,
        float radius,
        float height,
        int capsuleDir,
        float lowTwist = 0,
        float highTwist = 0,
        float swing = 0)
    {
        // Remove any existing ragdoll components so the tool is safe to re-run.
        foreach (CharacterJoint  j in bone.GetComponents<CharacterJoint>())  Undo.DestroyObjectImmediate(j);
        foreach (CapsuleCollider c in bone.GetComponents<CapsuleCollider>()) Undo.DestroyObjectImmediate(c);
        foreach (Rigidbody       r in bone.GetComponents<Rigidbody>())       Undo.DestroyObjectImmediate(r);

        // Rigidbody — starts kinematic so the Animator drives the bones normally.
        // NPCRagdoll sets isKinematic = false when a ragdoll is triggered.
        var rb = Undo.AddComponent<Rigidbody>(bone.gameObject);
        rb.mass           = mass;
        rb.linearDamping  = 0.05f;
        rb.angularDamping = 0.05f;
        rb.isKinematic    = true;

        // CapsuleCollider — starts disabled for the same reason.
        // Center is offset halfway toward the first child so it covers the limb
        // segment rather than sitting entirely at the joint pivot.
        var col = Undo.AddComponent<CapsuleCollider>(bone.gameObject);
        col.radius    = radius;
        col.height    = height;
        col.direction = capsuleDir;
        col.enabled   = false;

        if (bone.childCount > 0)
        {
            Vector3 toChild = bone.InverseTransformPoint(bone.GetChild(0).position);
            col.center = toChild * 0.5f;
        }

        if (connectedTo == null) return;

        // CharacterJoint chains this bone to its parent in the ragdoll hierarchy.
        var joint = Undo.AddComponent<CharacterJoint>(bone.gameObject);
        joint.connectedBody = connectedTo.GetComponent<Rigidbody>();

        SoftJointLimit lo = joint.lowTwistLimit;  lo.limit = lowTwist;  joint.lowTwistLimit  = lo;
        SoftJointLimit hi = joint.highTwistLimit; hi.limit = highTwist; joint.highTwistLimit = hi;
        SoftJointLimit s1 = joint.swing1Limit;    s1.limit = swing;     joint.swing1Limit    = s1;
    }
}
#endif