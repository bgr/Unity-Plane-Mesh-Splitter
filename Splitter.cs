using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: support multiple UV channels

namespace MeshGridSplitter
{
    public struct GridCoordinates
    {
        private static readonly float precision = 100;
        private Vector3Int value;

        public GridCoordinates(float x, float y, float z)
        {
            value = new Vector3Int(Mathf.RoundToInt(x * precision), Mathf.RoundToInt(y * precision), Mathf.RoundToInt(z * precision));
        }

        public Vector3 ToVector3()
        {
            return new Vector3((float)value.x / precision, (float)value.y / precision, (float)value.z / precision);
        }

        public override string ToString()
        {
            var vec = ToVector3();
            return string.Format("GridCoordinates({0}, {1}, {2})", vec.x, vec.y, vec.z);
        }
    }

    public static class Splitter
    {
        private class SplitterData
        {
            public MeshFilter sourceFilter;
            public MeshRenderer sourceRenderer;
            public Mesh sourceMesh;
            public Vector3[] sourceVertices;
            public int[] sourceTriangles;
            public Vector2[] sourceUvs;
            public Vector3[] sourceNormals;
            public float gridSize;
            public bool axisX;
            public bool axisY;
            public bool axisZ;
            public bool rebase;
            public bool allow32bitIndices;

            public SplitterData(MeshFilter source, float gridSize, bool axisX, bool axisY, bool axisZ, bool rebase, bool allow32bitIndices)
            {
                this.sourceFilter = source;
                this.gridSize = gridSize;
                this.axisX = axisX;
                this.axisY = axisY;
                this.axisZ = axisZ;
                this.rebase = rebase;
                this.allow32bitIndices = allow32bitIndices;

                sourceFilter = source;
                sourceRenderer = source.GetComponent<MeshRenderer>();
                sourceMesh = sourceFilter ? sourceFilter.sharedMesh : null;
                sourceVertices = sourceMesh ? sourceMesh.vertices : null;
                sourceTriangles = sourceMesh ? sourceMesh.triangles : null;
                sourceUvs = sourceMesh ? sourceMesh.uv : null;
                sourceNormals = sourceMesh ? sourceMesh.normals : null;

                Validate();
            }

            public void Validate()
            {
                if (sourceFilter == null)
                {
                    throw new ArgumentNullException("source");
                }
                if (sourceRenderer == null)
                {
                    throw new InvalidProgramException("Couldn't find a MeshRenderer on the given source");
                }
                if (sourceMesh == null)
                {
                    throw new InvalidProgramException("The sharedMesh on the given source is null");
                }
                if ((axisX ? 1 : 0) + (axisY ? 1 : 0) + (axisZ ? 1 : 0) < 1)
                {
                    throw new ArgumentException("At least one axis must be true");
                }
                if (gridSize <= Mathf.Epsilon)
                {
                    throw new ArgumentException("Grid size must be positive");
                }
            }
        }

        public static List<GameObject> Split(MeshFilter source, float gridSize, bool splitX, bool splitY, bool splitZ, bool rebase, Vector3 rebaseOrigin, bool allow32bitIndices)
        {
            var data = new SplitterData(source, gridSize, splitX, splitY, splitZ, rebase, allow32bitIndices);
            rebaseOrigin = rebase ? rebaseOrigin : source.transform.position;
            var triDict = MapTrianglesToGridNodes(data, rebaseOrigin);
            var result = new List<GameObject>();

            foreach (var kv in triDict)
            {
                var splitGO = CreateMesh(kv.Key, kv.Value, data);
                result.Add(splitGO);
            }

            return result;
        }

        private static GameObject CreateMesh(GridCoordinates gridCoordinates, List<int> dictTris, SplitterData data)
        {
            GameObject newObject = new GameObject();
            newObject.name = "SubMesh " + gridCoordinates;
            newObject.AddComponent<MeshFilter>();
            newObject.AddComponent<MeshRenderer>();

            newObject.transform.position = data.rebase ? gridCoordinates.ToVector3() : data.sourceFilter.transform.position;
            // if object position was snapped to grid position, we'll need to shift the vertices by the same amount
            var vertexOffset = newObject.transform.position - data.sourceFilter.transform.position;

            MeshRenderer newRenderer = newObject.GetComponent<MeshRenderer>();
            newRenderer.sharedMaterial = data.sourceRenderer.sharedMaterial;

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();

            for (int i = 0; i < dictTris.Count; i += 3)
            {
                verts.Add(data.sourceVertices[dictTris[i]] - vertexOffset);
                verts.Add(data.sourceVertices[dictTris[i + 1]] - vertexOffset);
                verts.Add(data.sourceVertices[dictTris[i + 2]] - vertexOffset);

                tris.Add(i);
                tris.Add(i + 1);
                tris.Add(i + 2);

                uvs.Add(data.sourceUvs[dictTris[i]]);
                uvs.Add(data.sourceUvs[dictTris[i + 1]]);
                uvs.Add(data.sourceUvs[dictTris[i + 2]]);

                normals.Add(data.sourceNormals[dictTris[i]]);
                normals.Add(data.sourceNormals[dictTris[i + 1]]);
                normals.Add(data.sourceNormals[dictTris[i + 2]]);
            }

            Mesh m = new Mesh();
            m.name = gridCoordinates.ToString();

            if (verts.Count >= 65535)
            {
                if (data.allow32bitIndices)
                {
                    Debug.LogWarning(string.Format("Mesh '{0}' will use 32-bit indices, it has {1} vertices", newObject.name, verts.Count), newObject);
                    m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }
                else
                {
                    Debug.LogWarning(string.Format("Mesh '{0}' has more than 65534 vertices, it'll probably look wrong. " +
                        "Consider using smaller grid size or enable `allow32bitIndices`", newObject.name), newObject);
                }
            }

            m.vertices = verts.ToArray();
            m.triangles = tris.ToArray();
            m.uv = uvs.ToArray();
            m.normals = normals.ToArray();
            m.RecalculateTangents();

#if UNITY_EDITOR
            UnityEditor.MeshUtility.Optimize(m);
#endif

            MeshFilter newMeshFilter = newObject.GetComponent<MeshFilter>();
            newMeshFilter.mesh = m;

            return newObject;
        }

        private static Dictionary<GridCoordinates, List<int>> MapTrianglesToGridNodes(SplitterData data, Vector3 origin)
        {
            /* Create a list of triangle indices from our mesh for every grid node */

            var triDictionary = new Dictionary<GridCoordinates, List<int>>();

            for (int i = 0; i < data.sourceTriangles.Length; i += 3)
            {
                // middle of the current triangle (average of its 3 verts).

                Vector3 currentPoint =
                    (data.sourceVertices[data.sourceTriangles[i]] +
                     data.sourceVertices[data.sourceTriangles[i + 1]] +
                     data.sourceVertices[data.sourceTriangles[i + 2]]) / 3;

                // grid coordinates should be relative to given origin world point

                Vector3 offset = data.sourceFilter.transform.position - origin;
                currentPoint += offset;

                // calculate coordinates of the closest grid node.

                currentPoint.x = Mathf.Floor(currentPoint.x / data.gridSize) * data.gridSize;
                currentPoint.y = Mathf.Floor(currentPoint.y / data.gridSize) * data.gridSize;
                currentPoint.z = Mathf.Floor(currentPoint.z / data.gridSize) * data.gridSize;

                // ignore an axis if its not enabled

                GridCoordinates gridPos = new GridCoordinates(
                    data.axisX ? currentPoint.x : 0,
                    data.axisY ? currentPoint.y : 0,
                    data.axisZ ? currentPoint.z : 0
                    );

                // check if the dictionary has a key (our grid position). Add it / create a list for it if it doesnt.

                if (!triDictionary.ContainsKey(gridPos))
                {
                    triDictionary.Add(gridPos, new List<int>());
                }

                // add these triangle indices to the list

                triDictionary[gridPos].Add(data.sourceTriangles[i]);
                triDictionary[gridPos].Add(data.sourceTriangles[i + 1]);
                triDictionary[gridPos].Add(data.sourceTriangles[i + 2]);
            }

            return triDictionary;
        }
    }
}
