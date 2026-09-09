using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

/// <summary>
/// Fakes a grab and pull on the VRCWakeMe handle during play mode, because nothing
/// in the editor can actually grab a PhysBone. The Av3Emulator will not let you edit
/// WakeMe_IsGrabbed or WakeMe_Stretch by hand, since the PhysBone drives them and
/// overwrites any edit on the next frame. This writes the bone's own grab state and
/// pushes it through the parameter accessors the emulator installs, from LateUpdate
/// so the bone's update cannot win.
///
/// Development only: copy into an avatar project, drop on the avatar root, enter play
/// mode with the emulator's "Enable Avatar OSC" on, then tick Grabbed and drag
/// Stretch. Remove before uploading.
/// </summary>
public class WakeHandleGrabSim : MonoBehaviour
{
    [Tooltip("Leave empty to find the handle automatically by its WakeMe parameter.")]
    public VRCPhysBone handle;

    [Tooltip("Simulates the grip. On its own this is not enough to wake the app.")]
    public bool grabbed;

    [Range(0f, 1f)]
    [Tooltip("Simulates the pull. The app wakes at 0.15 and above.")]
    public float stretch = 0.4f;

    private void Awake()
    {
        if (handle != null) return;

        foreach (var bone in GetComponentsInChildren<VRCPhysBone>(true))
        {
            if (bone.parameter != "WakeMe") continue;
            handle = bone;
            break;
        }

        if (handle == null) Debug.LogWarning("WakeHandleGrabSim: no PhysBone with parameter \"WakeMe\" found.");
    }

    private void LateUpdate()
    {
        if (handle == null) return;

        var pull = grabbed ? stretch : 0f;
        handle.param_IsGrabbedValue = grabbed;
        handle.param_StretchValue = pull;

        if (handle.param_IsGrabbed != null) handle.param_IsGrabbed.boolVal = grabbed;
        if (handle.param_Stretch != null) handle.param_Stretch.floatVal = pull;
    }
}
