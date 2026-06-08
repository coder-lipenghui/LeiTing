using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace LeiTing.Editor
{
    public sealed class DouyinWebGLAudioBuildGuard : IPreprocessBuildWithReport
    {
        private const string StarkBuilderSettingPath = "Assets/Editor/StarkBuilderSetting.asset";
        private const string UseByteAudioApiPropertyName = "useByteAudioAPI";
        private const string IsWebGL2PropertyName = "isWebGL2";

        public int callbackOrder => -1100;

        public void OnPreprocessBuild(BuildReport report)
        {
            EnsureByteAudioApiEnabled();
            EnsureWebGL2Enabled();
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

        [MenuItem("LeiTing/Build/Ensure WebGL2 Graphics API")]
        public static void EnsureWebGL2Enabled()
        {
            EnsureGraphicsApi(BuildTarget.WebGL);
            EnsureOptionalMiniGameGraphicsApi();
            EnsureStarkWebGL2Flag();
        }

        private static void EnsureGraphicsApi(BuildTarget target)
        {
            var graphicsApis = PlayerSettings.GetGraphicsAPIs(target);
            if (graphicsApis.Length == 1 && graphicsApis[0] == GraphicsDeviceType.OpenGLES3)
            {
                return;
            }

            PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
            PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.OpenGLES3 });
            Debug.Log($"Forced {target} Graphics API to OpenGLES3/WebGL2 for URP shader compatibility.");
        }

        private static void EnsureOptionalMiniGameGraphicsApi()
        {
            if (!System.Enum.TryParse("MiniGame", out BuildTarget miniGameTarget))
            {
                return;
            }

            try
            {
                EnsureGraphicsApi(miniGameTarget);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"MiniGame WebGL2 guard skipped: {exception.Message}");
            }
        }

        private static void EnsureStarkWebGL2Flag()
        {
            var setting = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(StarkBuilderSettingPath);
            if (setting == null)
            {
                Debug.LogWarning($"Douyin WebGL2 guard skipped; setting asset not found: {StarkBuilderSettingPath}");
                return;
            }

            var serializedSetting = new SerializedObject(setting);
            var isWebGL2 = serializedSetting.FindProperty(IsWebGL2PropertyName);
            if (isWebGL2 == null || isWebGL2.propertyType != SerializedPropertyType.Boolean)
            {
                Debug.LogWarning($"Douyin WebGL2 guard skipped; property not found: {IsWebGL2PropertyName}");
                return;
            }

            if (isWebGL2.boolValue)
            {
                return;
            }

            isWebGL2.boolValue = true;
            serializedSetting.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(setting);
            AssetDatabase.SaveAssets();
            Debug.Log("Enabled Douyin WebGL2 flag so the mini-game shell requests a WebGL2 context.");
        }
    }
}
