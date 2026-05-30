using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LeiTing.Editor
{
    public sealed class DouyinWebGLAudioBuildGuard : IPreprocessBuildWithReport
    {
        private const string StarkBuilderSettingPath = "Assets/Editor/StarkBuilderSetting.asset";
        private const string UseByteAudioApiPropertyName = "useByteAudioAPI";

        public int callbackOrder => -1100;

        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureByteAudioApiEnabled();
        }

        [MenuItem("LeiTing/Build/Ensure Douyin WebGL Audio")]
        public static void EnsureByteAudioApiEnabled()
        {
            var setting = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(StarkBuilderSettingPath);
            if (setting == null)
            {
                Debug.LogWarning($"Douyin WebGL audio guard skipped; setting asset not found: {StarkBuilderSettingPath}");
                return;
            }

            var serializedSetting = new SerializedObject(setting);
            var useByteAudioApi = serializedSetting.FindProperty(UseByteAudioApiPropertyName);
            if (useByteAudioApi == null || useByteAudioApi.propertyType != SerializedPropertyType.Boolean)
            {
                Debug.LogWarning($"Douyin WebGL audio guard skipped; property not found: {UseByteAudioApiPropertyName}");
                return;
            }

            if (useByteAudioApi.boolValue)
            {
                return;
            }

            useByteAudioApi.boolValue = true;
            serializedSetting.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
            AssetDatabase.SaveAssets();
            Debug.Log("Enabled Douyin WebGL ByteAudio API so all game audio follows iOS silent mode consistently.");
        }
    }
}
