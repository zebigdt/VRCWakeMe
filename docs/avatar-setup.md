# Avatar setup (Unity)

VRCWakeMe needs a grabbable **VRC PhysBone** handle on the avatar you sleep in. Someone grips the handle and pulls → VRChat sends `WakeMe_IsGrabbed` and `WakeMe_Stretch` over OSC → the app plays the alarm.

A poke does nothing, and that is the point: a hand brushing past you while you sleep cannot start the alarm. It takes a grip and a tug.

This page builds the handle by hand. If you would rather not, the [prefab](../README.md#avatar-setup) does steps 1 to 3 for you in one menu click; come back here for [Upload and check](#4-upload-and-check) and the troubleshooting below.

## See it in VRChat

These are different views, and the handle only shows in one of them:

| View | Menu | What it shows |
| --- | --- | --- |
| **PhysBones overlay** | Action Menu → **Options → Avatar → Avatar Overlay → PhysBones** | PhysBone chains and grab radii — the handle is here |
| **Contacts overlay** | Same menu → **Contacts** | Contact Senders and Receivers only, so the handle is **not** here |
| **Avatar debug** | Action Menu → **Options → Avatar → Debug** | Parameter names and values (not shapes) |

In Unity, the PhysBone gizmo appears only when **Gizmos** is on (Scene view, top right) **and** you have the handle object (or a parent) selected.

## 1. Create the handle bones

1. In the Hierarchy, select **Chest** or **UpperChest**.
2. Right-click → **Create Empty**. Name it `VRCWakeMe Handle`.
3. Right-click that object → **Create Empty**. Name the child `Handle Tip` and set its local position to `0, 0.06, 0`.
4. Leave both empty: no Mesh Filter, no Mesh Renderer, no materials.

The child is not optional. A PhysBone moves its child bones, so the tip is the part that travels when someone pulls, and the 6 cm gap is the rest length that the pull is measured against.

## 2. Add the PhysBone

Select `VRCWakeMe Handle` → **Add Component** → **VRC Phys Bone**. Set:

| Field | Value |
| --- | --- |
| Root Transform | leave empty (uses this object) |
| Integration Type | **Simplified** |
| Pull | `0.8` |
| Spring | `0.3` |
| Stiffness | `0.4` |
| Gravity | `0` |
| Gravity Falloff | `0` |
| Immobile Type | **World** |
| Immobile | `1` |
| Radius | `0.12` (meters) — the grab radius. If this is `0`, nobody can grab it |
| Allow Collision | **Off** |
| Allow Grabbing | **On** |
| Allow Posing | **Off** |
| Snap to Hand | **Off** |
| Grab Movement | `1` |
| Max Stretch | `1` |
| Max Squish | `0` |
| Parameter | `WakeMe` |

Three of those matter more than the rest:

- **Max Stretch `1`** is what makes a pull measurable. At `0` the bone cannot stretch, `WakeMe_Stretch` stays at zero forever, and the app will never see a pull.
- **Immobile `1`** means your own walking and running cannot swing the handle, so it stays quiet while you sleep.
- **Allow Posing Off** makes the handle spring back to your chest when released instead of staying where it was left.

## 3. Expression parameters (needed for OSC)

The PhysBone drives the animator without this, but VRChat OSC only sends parameters that exist on the avatar's **VRC Expression Parameters** asset. PhysBone writes its parameters with a suffix, so you add two, both **unsynced** and **unsaved**:

| Name | Type |
| --- | --- |
| `WakeMe_IsGrabbed` | **Bool** |
| `WakeMe_Stretch` | **Float** |

Add both. `WakeMe_IsGrabbed` alone still works, but then the alarm fires on the bare grab, without needing the pull.

## 4. Upload and check

1. Upload and wear the avatar.
2. Action Menu → **Options → Avatar → Avatar Overlay → PhysBones**. You should see the handle's sphere on your chest.
3. Action Menu → **Options → Avatar → Debug** and confirm `WakeMe_IsGrabbed` and `WakeMe_Stretch` are in the list. Grab the handle and pull: one flips to true, the other climbs.
4. Action Menu → **OSC → Enable**.
5. Start VRCWakeMe. Settings should switch to **Linked with VRChat**. Action Menu → **OSC → OSC Debug** shows tiles for `/VRCWakeMe/armed`, `/VRCWakeMe/disarmed`, `/VRCWakeMe/grabbed`, and `/VRCWakeMe/pulled`; the ones matching the current state keep flashing.
6. With **Activated** on, grab the handle and tug about a centimeter. `grabbed` and then `pulled` should flash, and the alarm plays.

If you cannot grab your own handle while testing, check the Quick Menu setting that lets you interact with your own avatar.

## Troubleshooting

**Nothing in the overlay.** You are on the Contacts overlay instead of PhysBones, the radius is `0`, the object is disabled, or the avatar was uploaded before you added the component.

**The `grabbed` tile flashes but `pulled` never does.** Pull further. The threshold is 15% of the bone's stretch range, roughly a centimeter on a 6 cm handle. If it never moves at all, **Max Stretch** is `0`, or **Grab Movement** is low enough that the bone drifts instead of following your hand.

**Both tiles flash but no alarm.** The app is disarmed, still inside its 20 second cooldown, or the alarm already ran its max duration.

**The overlay shows the handle but nobody can grab it.** **Allow Grabbing** is off, the radius is too small to find in VR, or the handle ended up inside your mesh — move it out to where a hand can reach.

## If OSC never fires

Delete this avatar's generated OSC config under `%LocalAppData%Low\VRChat\VRChat\OSC\`, rejoin, and enable OSC again.
