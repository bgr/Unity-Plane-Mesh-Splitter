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

            if (split.meshToSplit == null)
            {
                EditorGUILayout.HelpBox("Mesh to split is null", MessageType.Error);
            }
            else if (split.meshToSplit.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("MeshFilter has null sharedMesh", MessageType.Error);
            }
            else if (GUILayout.Button("Split"))
            {
                split.Split();
            }
        }
    }
}