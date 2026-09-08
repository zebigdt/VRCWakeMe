using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace VRCWakeMe.Editor
{
    /// <summary>
    /// Drops the grab handle onto an avatar and registers its PhysBone parameters
    /// on the Expression Parameters asset. VRChat only sends parameters over OSC if
    /// they are listed there, so the prefab alone is not enough.
    /// </summary>
    internal static class VRCWakeMeSetup
    {
        private const string Title = "VRCWakeMe";
        private const string HandlePrefabGuid = "7c1e6a2b4d3f4e8fa1b2c3d4e5f60711";
        private const string ObjectPrefix = "VRCWakeMe";
        private const string HandleParameter = "WakeMe";
        private const string GeneratedFolder = "Assets/VRCWakeMe/Generated";

        [MenuItem("Tools/VRCWakeMe/Add wake handle to avatar", false, 100)]
        private static void AddWakeHandle()
        {
            var avatar = SelectedAvatar();
            if (avatar == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "Select your avatar in the Hierarchy first (the object with the VRC Avatar Descriptor).", "OK");
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(HandlePrefabGuid);
            var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "Could not find \"VRCWakeMe Handle.prefab\". Re-import the VRCWakeMe folder into Assets and try again.",
                    "OK");
                return;
            }

            var parent = ChestOrRoot(avatar);
            var handle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            handle.transform.localPosition = Vector3.zero;
            handle.transform.localRotation = Quaternion.identity;
            handle.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(handle, "Add wake handle");
            Selection.activeGameObject = handle;
            EditorGUIUtility.PingObject(handle);

            RegisterParameters(avatar,
                $"Added the handle under \"{parent.name}\". Move it to where you want it grabbed, then upload.");
        }

        [MenuItem("Tools/VRCWakeMe/Register wake parameters on avatar", false, 101)]
        private static void RegisterWakeParameters()
        {
            var avatar = SelectedAvatar();
            if (avatar == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "Select your avatar in the Hierarchy first (the object with the VRC Avatar Descriptor).", "OK");
                return;
            }

            RegisterParameters(avatar, null);
        }

        private static void RegisterParameters(VRCAvatarDescriptor avatar, string prefix)
        {
            var names = WakeParameterNames(avatar);
            if (names.Count == 0)
            {
                EditorUtility.DisplayDialog(Title,
                    "No VRCWakeMe handle was found under this avatar. Add the handle prefab first.", "OK");
                return;
            }

            var asset = avatar.expressionParameters;
            if (asset == null)
            {
                if (!EditorUtility.DisplayDialog(Title,
                        "This avatar has no Expression Parameters asset. Create one now?\n\n" +
                        "It will include VRChat's default parameters plus the VRCWakeMe parameters.",
                        "Create", "Cancel"))
                {
                    return;
                }

                asset = CreateParametersAsset(avatar);
                if (asset == null) return;
            }

            var parameters = (asset.parameters ?? new VRCExpressionParameters.Parameter[0]).ToList();
            var added = new List<string>();
            foreach (var (name, type) in names)
            {
                if (parameters.Any(p => p != null && p.name == name)) continue;
                parameters.Add(Parameter(name, type, synced: false));
                added.Add(name);
            }

            if (added.Count > 0)
            {
                Undo.RecordObject(asset, "Register wake parameters");
                asset.parameters = parameters.ToArray();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }

            var summary = added.Count > 0
                ? $"Added to Expression Parameters (unsynced, unsaved):\n  {string.Join("\n  ", added)}"
                : $"Expression Parameters already had:\n  {string.Join("\n  ", names.Select(n => n.Name))}";
            EditorUtility.DisplayDialog(Title, prefix == null ? summary : $"{prefix}\n\n{summary}", "OK");
        }

        /// <summary>
        /// Only picks up our own PhysBone so unrelated ones (hair, ears, tails, ...)
        /// never get pushed into the parameter list.
        /// </summary>
        private static List<(string Name, VRCExpressionParameters.ValueType Type)> WakeParameterNames(
            VRCAvatarDescriptor avatar)
        {
            var names = new List<(string Name, VRCExpressionParameters.ValueType Type)>();

            void Add(string name, VRCExpressionParameters.ValueType type)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                if (names.Any(n => n.Name == name)) return;
                names.Add((name, type));
            }

            foreach (var bone in avatar.GetComponentsInChildren<VRCPhysBone>(true))
            {
                if (!IsOurs(bone.gameObject, bone.parameter)) continue;
                if (string.IsNullOrWhiteSpace(bone.parameter)) continue;

                // The grab drives the alarm; the stretch is what makes a pull
                // distinguishable from a bump, so both have to reach OSC.
                Add(bone.parameter + "_IsGrabbed", VRCExpressionParameters.ValueType.Bool);
                Add(bone.parameter + "_Stretch", VRCExpressionParameters.ValueType.Float);
            }

            return names;
        }

        private static bool IsOurs(GameObject owner, string parameter) =>
            owner.name.StartsWith(ObjectPrefix, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameter, HandleParameter, System.StringComparison.OrdinalIgnoreCase);

        private static Transform ChestOrRoot(VRCAvatarDescriptor avatar)
        {
            var animator = avatar.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return avatar.transform;

            var bone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (bone == null) bone = animator.GetBoneTransform(HumanBodyBones.Spine);
            return bone == null ? avatar.transform : bone;
        }

        private static VRCExpressionParameters CreateParametersAsset(VRCAvatarDescriptor avatar)
        {
            Directory.CreateDirectory(GeneratedFolder);
            AssetDatabase.Refresh();

            var asset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            asset.parameters = new[]
            {
                Parameter("VRCEmote", VRCExpressionParameters.ValueType.Int, true),
                Parameter("VRCFaceBlendH", VRCExpressionParameters.ValueType.Float, true),
                Parameter("VRCFaceBlendV", VRCExpressionParameters.ValueType.Float, true)
            };

            var path = AssetDatabase.GenerateUniqueAssetPath($"{GeneratedFolder}/{avatar.name} Parameters.asset");
            AssetDatabase.CreateAsset(asset, path);

            Undo.RecordObject(avatar, "Assign expression parameters");
            avatar.customExpressions = true;
            avatar.expressionParameters = asset;
            EditorUtility.SetDirty(avatar);
            return asset;
        }

        private static VRCExpressionParameters.Parameter Parameter(
            string name, VRCExpressionParameters.ValueType type, bool synced) =>
            new VRCExpressionParameters.Parameter
            {
                name = name,
                valueType = type,
                defaultValue = 0f,
                saved = false,
                networkSynced = synced
            };

        private static VRCAvatarDescriptor SelectedAvatar()
        {
            var selection = Selection.activeGameObject;
            if (selection == null) return null;
            return selection.GetComponent<VRCAvatarDescriptor>() ??
                   selection.GetComponentInParent<VRCAvatarDescriptor>();
        }
    }
}
