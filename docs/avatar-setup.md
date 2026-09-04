# Avatar setup (Unity)

VRCWakeMe listens for one OSC parameter: `/avatar/parameters/WakeMe/Touched`.

You add a **VRC Contact Receiver** on the avatar you sleep in. This repo does not ship a Unity prefab.

## Contact receiver

1. Create an empty GameObject on the chest, shoulder, or head. Name it `WakeMeContact`.
2. Add **VRC Contact Receiver**.
3. Shape: Sphere, radius large enough to poke easily (about 0.15–0.3 m). Contacts cannot exceed 3 m radius.
4. Collision tags: `Hand` and `Finger` (built-in senders on other avatars).
5. **Allow Others**: on.
6. **Allow Self**: off (so you do not wake yourself).
7. **Local Only**: on (runs only on your client, does not cost remote performance rank).
8. Receiver type: **Constant**.
9. Parameter: `WakeMe/Touched`.
10. Use a **Bool** parameter.

Constant is required. **OnEnter** is true for a single animator frame and is easy for OSC to miss.

## Expression parameters

1. Open your **VRC Expression Parameters** asset.
2. Add `WakeMe/Touched` as **Bool**.
3. Leave **Synced** off (does not use the 256-bit sync budget).
4. Leave **Saved** off.

The name must match the contact parameter exactly, including the slash. VRChat then includes it in the OSC config it generates for the avatar.

## Upload and enable OSC

1. Upload the avatar and wear it in VRChat.
2. Action Menu → **OSC** → **Enable**.
3. Start VRCWakeMe. You should see a HUD notice that VRChat is sending OSC to VRCWakeMe.
4. Arm the tray icon and have someone poke the contact (or test with a friend’s hand collider).

If the parameter never arrives, delete the generated OSC config for this avatar under `%LocalAppData%Low\VRChat\VRChat\OSC\` so VRChat regenerates it after you rejoin with the avatar.

## Optional: grab handle instead of a poke

Not required for v1. A PhysBone with parameter prefix `WakeMe` exposes `WakeMe_IsGrabbed`. That is a different OSC address; the app currently listens only to `WakeMe/Touched`. You can drive the same bool from a PhysBone grab with an animator parameter driver if you want a grabable charm.
