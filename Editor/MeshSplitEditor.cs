using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MeshGridSplitter
{
    [CustomEditor(typeof(MeshSplit))]
    public class MeshSplitEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MeshSplit split = (MeshSplit)target;

            if (GUILayout.Button("Populate from children MeshFilters"))
            {
                if (split.meshesToSplit == null) split.meshesToSplit = new List<MeshFilter>();
                split.meshesToSplit.AddRange(split.GetComponentsInChildren<MeshFilter>(split.populateIncludeDisabled));
            }
            split.populateIncludeDisabled = GUILayout.Toggle(split.populateIncludeDisabled, "Include meshes from disabled objects");

            DrawDefaultInspector();

            if (split.meshesToSplit == null || split.meshesToSplit.Count == 0)
            {
                EditorGUILayout.HelpBox("No meshes to split", MessageType.Error);
                return;
            }
            if (split.meshesToSplit.Any(m => m == null))
            {
                EditorGUILayout.HelpBox("One of the input MeshFilters is null", MessageType.Error);
                return;
            }
            if (split.meshesToSplit.Any(m => m.sharedMesh == null))
            {
                EditorGUILayout.HelpBox("One of the input MeshFilters has null sharedMesh", MessageType.Error);
                return;
            }

            if (split.drawGrid)
            {
                EditorGUILayout.HelpBox("Note: preview grid might not be lined-up with actual split result", MessageType.None);
            }


            if (GUILayout.Button("Split"))
            {
                split.Split();
            }
        }
    }
}