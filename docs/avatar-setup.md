# Avatar setup (Unity)

Add an **invisible** grab point to the avatar you sleep in. Other players grab that spot to wake you. Do not add a mesh, cube, or charm.

VRCWakeMe listens for one bool: `grabbed_IsGrabbed`.

## 1. Create the invisible handle

1. In the Hierarchy, select a body bone (Chest or UpperChest is fine).
2. Right-click it → **Create Empty**. Name it `WakeMeGrab`.
3. Right-click `WakeMeGrab` → **Create Empty**. Name the child `WakeMeGrabEnd`.
4. Select `WakeMeGrabEnd` and in the Transform, set **Position** to `0, 0, 0.05` (local). That gives PhysBone a short bone to grab.
5. Leave both objects empty. No Mesh Filter, no Mesh Renderer, no materials.

In the Scene view, turn on **Gizmos** so you can see the grab radius while you place it. It will not be visible in VRChat.

## 2. Add the PhysBone

Select `WakeMeGrab` and **Add Component** → **VRC Phys Bone**. Set these fields:

| Field | Value |
| --- | --- |
| Root Transform | `WakeMeGrab` |
| Radius | `0.12` (invisible grab size, in meters) |
| Immobile | `1` |
| Gravity | `0` |
| Allow Grabbing | On |
| Allow Posing | Off |
| Allow Collision | Off |
| Parameter | `grabbed` |

VRChat always names the grab flag `{Parameter}_IsGrabbed`. With Parameter `grabbed`, that flag is `grabbed_IsGrabbed`. That is the only parameter the app uses.

## 3. Add the expression parameter

1. Open your avatar’s **VRC Expression Parameters** asset.
2. Add one **Bool** named `grabbed_IsGrabbed` (copy that name exactly).
3. Uncheck **Synced**.
4. Uncheck **Saved**.

Do not add a parameter named `grabbed`. PhysBone does not write to that name.

## 4. Upload and test

1. Upload the avatar and wear it in VRChat.
2. Action Menu → **OSC** → **Enable**.
3. Start VRCWakeMe and turn **Activated** on.
4. Have someone **grab** the invisible point (grip, not a poke). They need to grab near where you parented `WakeMeGrab`.

If nothing happens, delete this avatar’s generated OSC config under `%LocalAppData%Low\VRChat\VRChat\OSC\`, rejoin, and try again.
