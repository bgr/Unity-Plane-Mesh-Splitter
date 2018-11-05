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

            DrawDefaultInspector();

            if (split.meshesToSplit == null || split.meshesToSplit.Length == 0)
            {
                EditorGUILayout.HelpBox("No meshes to split", MessageType.Error);
            }
            else if (split.meshesToSplit.Any(m => m == null))
            {
                EditorGUILayout.HelpBox("One of the input MeshFilters is null", MessageType.Error);
            }
            else if (split.meshesToSplit.Any(m => m.sharedMesh == null))
            {
                EditorGUILayout.HelpBox("One of the input MeshFilters has null sharedMesh", MessageType.Error);
            }
            else if (GUILayout.Button("Split"))
            {
                split.Split();
            }
        }
    }
}