// original credits: "Made by Artur Nasiadko - https://github.com/artnas"
// modified by bgr - https://github.com/bgr

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MeshGridSplitter
{
    public class MeshSplit : MonoBehaviour
    {
        public MeshFilter[] meshesToSplit;

        // draw debug grid when the object is selected
        public bool drawGrid = true;

        public float gridSize = 16;

        // which axes should the script use?
        public bool axisX = true;
        public bool axisY = true;
        public bool axisZ = true;

        public void Split()
        {
            Splitter.Split(
                meshesToSplit,
                gridSize,
                axisX, axisY, axisZ
                );
        }

        Bounds GlobalBounds(MeshFilter mf)
        {
            Bounds b = mf.sharedMesh.bounds;
            b.center += mf.transform.position;
            return b;
        }

        void OnDrawGizmosSelected()
        {
            if (drawGrid && meshesToSplit != null && meshesToSplit.Length > 0)
            {
                Bounds b = GlobalBounds(meshesToSplit[0]);
                foreach (var m in meshesToSplit) b.Encapsulate(GlobalBounds(m));

                float xSize = Mathf.Ceil(b.extents.x) + gridSize;
                float ySize = Mathf.Ceil(b.extents.y) + gridSize;
                float zSize = Mathf.Ceil(b.extents.z) + gridSize;

                for (float z = -zSize; z <= zSize; z += gridSize)
                {
                    for (float y = -ySize; y <= ySize; y += gridSize)
                    {
                        for (float x = -xSize; x <= xSize; x += gridSize)
                        {
                            Vector3 position = b.center + new Vector3(x, y, z);
                            Gizmos.DrawWireCube(position, gridSize * Vector3.one);
                        }
                    }
                }
            }
        }
    }
}
