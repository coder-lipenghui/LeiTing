#if UNITY_EDITOR
using LeiTing.Enemy.Movement;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemySplinePath))]
[CanEditMultipleObjects]
public class EnemySplinePathEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Alias Tools", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Generate aliases from child SplineContainer GameObject names. If there are no child SplineContainers, aliases fall back to spline1, spline2, spline3... on this object's SplineContainer.", MessageType.Info);

            if (GUILayout.Button("Auto Rebuild Aliases", GUILayout.Height(28f)))
            {
                RebuildAliases();
            }
        }
    }

    private void RebuildAliases()
    {
        foreach (var targetObject in targets)
        {
            if (targetObject is not EnemySplinePath path)
            {
                continue;
            }

            Undo.RecordObject(path, "Auto Rebuild Spline Aliases");
            var count = path.RebuildSequentialAliases();
            EditorUtility.SetDirty(path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(path);
            Debug.Log($"Rebuilt {count} spline aliases on {path.name}.", path);
        }
    }
}
#endif
