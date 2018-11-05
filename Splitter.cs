﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public override string ToString()
        {
            return string.Format("({0},{1},{2})", (float)value.x / precision, (float)value.y / precision, (float)value.z / precision);
        }
    }

    public class Splitter
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

            public SplitterData(MeshFilter source, float gridSize, bool axisX, bool axisY, bool axisZ)
            {
                this.sourceFilter = source;
                this.gridSize = gridSize;
                this.axisX = axisX;
                this.axisY = axisY;
                this.axisZ = axisZ;

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

        public static void Split(MeshFilter[] sources, float gridSize, bool splitX, bool splitY, bool splitZ)
        {
            sources
                .ToList()
                .ForEach(s => Split(s, gridSize, splitX, splitY, splitZ));
        }

        public static void Split(MeshFilter source, float gridSize, bool splitX, bool splitY, bool splitZ)
        {
            var data = new SplitterData(source, gridSize, splitX, splitY, splitZ);

            var triDict = MapTrianglesToGridNodes(data);

            var resultGO = new GameObject("[split meshes]");
            resultGO.transform.position = source.transform.position;

            foreach (var kv in triDict)
            {
                var splitGO = CreateMesh(kv.Key, kv.Value, data);
                splitGO.transform.SetParent(resultGO.transform, worldPositionStays: false);
            }
        }

        private static GameObject CreateMesh(GridCoordinates gridCoordinates, List<int> dictTris, SplitterData data)
        {
            GameObject newObject = new GameObject();
            newObject.name = "SubMesh " + gridCoordinates;
            newObject.AddComponent<MeshFilter>();
            newObject.AddComponent<MeshRenderer>();

            MeshRenderer newRenderer = newObject.GetComponent<MeshRenderer>();
            newRenderer.sharedMaterial = data.sourceRenderer.sharedMaterial;

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> normals = new List<Vector3>();

            for (int i = 0; i < dictTris.Count; i += 3)
            {
                verts.Add(data.sourceVertices[dictTris[i]]);
                verts.Add(data.sourceVertices[dictTris[i + 1]]);
                verts.Add(data.sourceVertices[dictTris[i + 2]]);

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

        private static Dictionary<GridCoordinates, List<int>> MapTrianglesToGridNodes(SplitterData data)
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

                // calculate coordinates of the closest grid node.

                currentPoint.x = Mathf.Round(currentPoint.x / data.gridSize) * data.gridSize;
                currentPoint.y = Mathf.Round(currentPoint.y / data.gridSize) * data.gridSize;
                currentPoint.z = Mathf.Round(currentPoint.z / data.gridSize) * data.gridSize;

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
