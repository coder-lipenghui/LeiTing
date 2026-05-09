using LeiTing.Missiles;
using UnityEditor;
using UnityEngine;

namespace LeiTing.EditorTools
{
    public class MissileVisualPreviewWindow : EditorWindow
    {
        private const string WindowTitle = "Missile Visual Preview";

        private GameObject selectedPrefab;
        private PreviewRenderUtility previewUtility;
        private GameObject previewInstance;
        private MissileVisualTrailMode configPreviewTrailMode = MissileVisualTrailMode.Light;
        private Color configPreviewTailColor = new Color(1f, 0.72f, 0.22f, 1f);
        private bool playing = true;
        private float previewSpeed = 1f;
        private float previewTime;
        private float lastCycle;
        private double lastTimestamp;
        private Vector2 scrollPosition;

        [MenuItem("LeiTing/Missiles/Visual Preview")]
        public static void Open()
        {
            GetWindow<MissileVisualPreviewWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            lastTimestamp = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            TryUseSelection();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreviewInstance();

            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void OnSelectionChange()
        {
            if (TryUseSelection())
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUI.BeginChangeCheck();
            var nextPrefab = (GameObject)EditorGUILayout.ObjectField("Missile Prefab", selectedPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedPrefab = nextPrefab;
                ReloadPreview();
            }

            EditorGUI.BeginChangeCheck();
            configPreviewTrailMode = (MissileVisualTrailMode)EditorGUILayout.EnumPopup("Config Trail Preview", configPreviewTrailMode);
            configPreviewTailColor = EditorGUILayout.ColorField("Config Tail Color", configPreviewTailColor);
            previewSpeed = EditorGUILayout.Slider("Preview Speed", previewSpeed, 0.1f, 3f);
            playing = EditorGUILayout.Toggle("Play", playing);
            if (EditorGUI.EndChangeCheck())
            {
                ReloadPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Preview"))
                {
                    ReloadPreview();
                }

                using (new EditorGUI.DisabledScope(selectedPrefab == null))
                {
                    if (GUILayout.Button("Add/Refresh Effect Objects On Prefab"))
                    {
                        AddOrRefreshVisualsOnSelectedPrefab();
                    }
                }
            }

            var assetEffects = selectedPrefab != null ? selectedPrefab.GetComponent<MissileVisualEffects>() : null;
            if (selectedPrefab != null && assetEffects == null)
            {
                EditorGUILayout.HelpBox("This prefab does not have MissileVisualEffects yet. Use the button above to add editable Flame_Particle, Smoke_Particle, Spark_Particle, and TrailRenderer objects.", MessageType.Info);
            }
            else if (assetEffects != null)
            {
                DrawEffectSettings(assetEffects);
            }

            GUILayout.Space(6f);
            var rect = GUILayoutUtility.GetRect(10f, 10000f, 240f, 10000f);
            DrawPreview(rect);

            EditorGUILayout.EndScrollView();
        }

        private void DrawEffectSettings(MissileVisualEffects assetEffects)
        {
            GUILayout.Space(6f);
            EditorGUILayout.LabelField("Editable Effect Parameters", EditorStyles.boldLabel);

            var serializedEffects = new SerializedObject(assetEffects);
            serializedEffects.Update();

            var changed = false;
            changed |= DrawProperty(serializedEffects, "trailMode", "Trail Mode");
            changed |= DrawProperty(serializedEffects, "useConfigTailColor", "Use Config Tail Color");
            changed |= DrawProperty(serializedEffects, "customTailColor", "Custom Tail Color");

            GUILayout.Space(4f);
            changed |= DrawProperty(serializedEffects, "flame", "Flame_Particle");
            changed |= DrawProperty(serializedEffects, "smoke", "Smoke_Particle");
            changed |= DrawProperty(serializedEffects, "spark", "Spark_Particle");
            changed |= DrawProperty(serializedEffects, "lightTrail", "TrailRenderer");
            changed |= DrawProperty(serializedEffects, "tailGlow", "Tail Glow");

            if (changed && serializedEffects.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(assetEffects);
                AssetDatabase.SaveAssets();
                ReloadPreview();
            }
        }

        private static bool DrawProperty(SerializedObject serializedObject, string propertyPath, string label)
        {
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return false;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent(label), true);
            return EditorGUI.EndChangeCheck();
        }

        private void DrawPreview(Rect rect)
        {
            if (selectedPrefab == null)
            {
                EditorGUI.HelpBox(rect, "Select a missile prefab to preview.", MessageType.Info);
                return;
            }

            EnsurePreviewUtility();
            EnsurePreviewInstance();

            if (previewInstance == null)
            {
                EditorGUI.HelpBox(rect, "Unable to create a preview instance.", MessageType.Warning);
                return;
            }

            StepPreview();

            previewUtility.BeginPreview(rect, GUIStyle.none);
            var camera = previewUtility.camera;
            camera.orthographic = true;
            camera.orthographicSize = 1.15f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.085f, 0.1f, 1f);
            camera.transform.position = new Vector3(0f, 0f, -6f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.Render();

            var texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private void EnsurePreviewUtility()
        {
            if (previewUtility != null)
            {
                return;
            }

            previewUtility = new PreviewRenderUtility();
            previewUtility.camera.orthographic = true;
        }

        private void EnsurePreviewInstance()
        {
            if (previewInstance != null || selectedPrefab == null)
            {
                return;
            }

            previewInstance = Instantiate(selectedPrefab);
            previewInstance.name = $"{selectedPrefab.name}_Preview";
            SetHideFlagsRecursively(previewInstance, HideFlags.HideAndDontSave);

            var controller = previewInstance.GetComponent<MissileController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            var body = previewInstance.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.simulated = false;
            }

            var effects = previewInstance.GetComponent<MissileVisualEffects>();
            if (effects == null)
            {
                effects = previewInstance.AddComponent<MissileVisualEffects>();
            }

            effects.EnsureEffectObjects();
            SetHideFlagsRecursively(previewInstance, HideFlags.HideAndDontSave);
            previewUtility.AddSingleGO(previewInstance);
            ApplyPreviewSettings(effects);
        }

        private void ApplyPreviewSettings(MissileVisualEffects effects)
        {
            if (effects == null)
            {
                return;
            }

            previewTime = 0f;
            lastCycle = 0f;
            previewInstance.transform.position = Vector3.zero;
            previewInstance.transform.rotation = Quaternion.identity;

            effects.Apply(new MissileVisualEffectContext
            {
                Radius = ResolveRadius(previewInstance),
                CanBeDestroyed = false,
                TailColor = configPreviewTailColor,
                TailType = ResolvePreviewTailType(configPreviewTrailMode),
                Time = previewTime
            });
            effects.Play();
        }

        private void StepPreview()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Mathf.Clamp((float)(now - lastTimestamp), 0f, 0.05f);
            lastTimestamp = now;

            var effects = previewInstance.GetComponent<MissileVisualEffects>();
            if (effects == null)
            {
                return;
            }

            if (playing)
            {
                previewTime += delta * previewSpeed;
            }

            var cycle = playing ? Mathf.Repeat(previewTime * 0.55f, 1f) : 0.55f;
            if (playing && cycle < lastCycle)
            {
                effects.StopAndClear();
                effects.Play();
            }

            lastCycle = cycle;

            var y = Mathf.Lerp(-0.45f, 0.45f, cycle);
            previewInstance.transform.position = new Vector3(0f, y, 0f);
            previewInstance.transform.rotation = Quaternion.identity;

            if (playing)
            {
                var lightTrail = effects.LightTrail;
                if (lightTrail != null && lightTrail.enabled && effects.LightTrailRoot != null)
                {
                    lightTrail.AddPosition(effects.LightTrailRoot.position);
                }

                var particleSystems = previewInstance.GetComponentsInChildren<ParticleSystem>();
                for (var index = 0; index < particleSystems.Length; index++)
                {
                    particleSystems[index].Simulate(delta * previewSpeed, true, false, true);
                }
            }

            effects.UpdateDynamic(previewTime);
        }

        private void ReloadPreview()
        {
            CleanupPreviewInstance();
            previewTime = 0f;
            lastCycle = 0f;
            lastTimestamp = EditorApplication.timeSinceStartup;
            Repaint();
        }

        private void CleanupPreviewInstance()
        {
            if (previewInstance != null)
            {
                DestroyImmediate(previewInstance);
                previewInstance = null;
            }
        }

        private void AddOrRefreshVisualsOnSelectedPrefab()
        {
            if (selectedPrefab == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(selectedPrefab);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var effects = prefabRoot.GetComponent<MissileVisualEffects>();
                var created = effects == null;
                if (created)
                {
                    effects = prefabRoot.AddComponent<MissileVisualEffects>();
                    effects.ResetToDefaults(configPreviewTrailMode, configPreviewTailColor, ResolveRadius(prefabRoot));
                }
                else
                {
                    effects.EnsureEffectObjects();
                    effects.Apply(new MissileVisualEffectContext
                    {
                        Radius = ResolveRadius(prefabRoot),
                        TailColor = configPreviewTailColor,
                        TailType = ResolvePreviewTailType(configPreviewTrailMode),
                        Time = 0f
                    });
                    effects.StopAndClear();
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            selectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            ReloadPreview();
        }

        private bool TryUseSelection()
        {
            var selected = Selection.activeObject as GameObject;
            if (selected == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/Prefabs/Missiles"))
            {
                return false;
            }

            if (selectedPrefab == selected)
            {
                return false;
            }

            selectedPrefab = selected;
            ReloadPreview();
            return true;
        }

        private void OnEditorUpdate()
        {
            if (playing && selectedPrefab != null)
            {
                Repaint();
            }
        }

        private static float ResolveRadius(GameObject target)
        {
            var collider = target.GetComponent<CircleCollider2D>();
            return collider != null ? Mathf.Max(0.04f, collider.radius) : 0.16f;
        }

        private static string ResolvePreviewTailType(MissileVisualTrailMode mode)
        {
            switch (mode)
            {
                case MissileVisualTrailMode.None:
                    return "none";
                case MissileVisualTrailMode.Smoke:
                    return "smoke";
                case MissileVisualTrailMode.LightAndSmoke:
                    return "fire_smoke";
                default:
                    return "light";
            }
        }

        private static void SetHideFlagsRecursively(GameObject target, HideFlags hideFlags)
        {
            target.hideFlags = hideFlags;
            foreach (Transform child in target.transform)
            {
                SetHideFlagsRecursively(child.gameObject, hideFlags);
            }
        }
    }
}
