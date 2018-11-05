using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO: support multiple UV channels

namespace MeshGridSplitter
{
    // a struct which will act as a key in our dictionary.
    // a Vector3 could probably be used instead, but I wanted to use ints
    public struct GridCoordinates
    {
        private static readonly float precision = 100;

        public int x, y, z;

        public GridCoordinates(float x, float y, float z)
        {
            this.x = Mathf.RoundToInt(x * precision);
            this.y = Mathf.RoundToInt(y * precision);
            this.z = Mathf.RoundToInt(z * precision);
        }

        public static implicit operator GridCoordinates(Vector3 v)
        {
            return new GridCoordinates((int)v.x, (int)v.y, (int)v.z);
        }
        public static implicit operator Vector3(GridCoordinates i)
        {
            return new Vector3(i.x, i.y, i.z);
        }

        public override string ToString()
        {
            return string.Format("({0},{1},{2})", (float)x / precision, (float)y / precision, (float)z / precision);
        }
    }

    public class Splitter
    {
        public float gridSize = 16;

        public bool axisX = true;
        public bool axisY = true;
        public bool axisZ = true;

        private MeshRenderer sourceRenderer;
        private Mesh sourceMesh;

        private Vector3[] baseVertices;
        private int[] baseTriangles;
        private Vector2[] baseUvs;
        private Vector3[] baseNormals;

        // this dictionary holds a list of triangle indices for every grid node
        private Dictionary<GridCoordinates, List<int>> triDictionary;

        public Splitter(MeshFilter source, float gridSize, bool splitX, bool splitY, bool splitZ)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (GetUsedAxisCount() < 1)
            {
                throw new ArgumentException("You have to choose at least 1 axis.");
            }
            if (gridSize <= Mathf.Epsilon)
            {
                throw new ArgumentException("Grid size must be positive");
            }

            sourceRenderer = source.GetComponent<MeshRenderer>();
            sourceMesh = source.sharedMesh;

            if (sourceRenderer == null)
            {
                throw new InvalidProgramException("Couldn't find a MeshRenderer on the given source");
            }
            if (sourceMesh == null)
            {
                throw new InvalidProgramException("The sharedMesh on the given source is null");
            }

            this.gridSize = gridSize;
            axisX = splitX;
            axisY = splitY;
            axisZ = splitZ;
        }

        public GameObject Split()
        {
            baseVertices = sourceMesh.vertices;
            baseTriangles = sourceMesh.triangles;
            baseUvs = sourceMesh.uv;
            baseNormals = sourceMesh.normals;

            MapTrianglesToGridNodes();

            var resultGO = new GameObject("[split meshes]");
            resultGO.transform.position = sourceRenderer.transform.position;

            foreach (var item in triDictionary.Keys)
            {
                var splitGO = CreateMesh(item, triDictionary[item]);
                splitGO.transform.SetParent(resultGO.transform, worldPositionStays: false);
            }

            return resultGO;
        }

        public GameObject CreateMesh(GridCoordinates gridCoordinates, List<int> dictionaryTriangles)
        {
            GameObject newObject = new GameObject();
            newObject.name = "SubMesh " + gridCoordinates;
            newObject.AddComponent<MeshFilter>();
            newObject.AddComponent<MeshRenderer>();

            MeshRenderer newRenderer = newObject.GetComponent<MeshRenderer>();
            newRenderer.sharedMaterial = sourceRenderer.sharedMaterial;

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();

            for (int i = 0; i < dictionaryTriangles.Count; i += 3)
            {
                verts.Add(baseVertices[dictionaryTriangles[i]]);
                verts.Add(baseVertices[dictionaryTriangles[i + 1]]);
                verts.Add(baseVertices[dictionaryTriangles[i + 2]]);

                tris.Add(i);
                tris.Add(i + 1);
                tris.Add(i + 2);

                uvs.Add(baseUvs[dictionaryTriangles[i]]);
                uvs.Add(baseUvs[dictionaryTriangles[i + 1]]);
                uvs.Add(baseUvs[dictionaryTriangles[i + 2]]);

                normals.Add(baseNormals[dictionaryTriangles[i]]);
                normals.Add(baseNormals[dictionaryTriangles[i + 1]]);
                normals.Add(baseNormals[dictionaryTriangles[i + 2]]);
            }

            Mesh m = new Mesh();
            m.name = gridCoordinates.ToString();

            if (verts.Count >= 65535)
            {
                Debug.LogWarning("Number of vertices is larger than 65534, mesh might look wrong. Consider using smaller grid size.");
                // TODO add option to allow 32-bit indices
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

        private void MapTrianglesToGridNodes()
        {
            /* Create a list of triangle indices from our mesh for every grid node */

            triDictionary = new Dictionary<GridCoordinates, List<int>>();

            for (int i = 0; i < baseTriangles.Length; i += 3)
            {
                // middle of the current triangle (average of its 3 verts).

                Vector3 currentPoint =
                    (baseVertices[baseTriangles[i]] +
                     baseVertices[baseTriangles[i + 1]] +
                     baseVertices[baseTriangles[i + 2]]) / 3;

                // calculate coordinates of the closest grid node.

                currentPoint.x = Mathf.Round(currentPoint.x / gridSize) * gridSize;
                currentPoint.y = Mathf.Round(currentPoint.y / gridSize) * gridSize;
                currentPoint.z = Mathf.Round(currentPoint.z / gridSize) * gridSize;

                // ignore an axis if its not enabled

                GridCoordinates gridPos = new GridCoordinates(
                    axisX ? currentPoint.x : 0,
                    axisY ? currentPoint.y : 0,
                    axisZ ? currentPoint.z : 0
                    );

                // check if the dictionary has a key (our grid position). Add it / create a list for it if it doesnt.

                if (!triDictionary.ContainsKey(gridPos))
                {
                    triDictionary.Add(gridPos, new List<int>());
                }

                // add these triangle indices to the list

                triDictionary[gridPos].Add(baseTriangles[i]);
                triDictionary[gridPos].Add(baseTriangles[i + 1]);
                triDictionary[gridPos].Add(baseTriangles[i + 2]);
            }
        }

        private int GetUsedAxisCount()
        {
            return (axisX ? 1 : 0) + (axisY ? 1 : 0) + (axisZ ? 1 : 0);
        }
    }
}
