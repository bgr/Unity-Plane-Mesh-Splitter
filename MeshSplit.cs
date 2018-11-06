// original credits: "Made by Artur Nasiadko - https://github.com/artnas"
// modified by bgr - https://github.com/bgr

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MeshGridSplitter
{
    public class MeshSplit : MonoBehaviour
    {
        public List<MeshFilter> meshesToSplit;

        // draw debug grid when the object is selected
        public bool drawGrid = true;

        public float gridSize = 16;

        // which axes should the script use?
        public bool axisX = true;
        public bool axisY = true;
        public bool axisZ = true;

        [Tooltip("If enabled, each split gameobject's pivot will be placed at its grid coordinates, otherwise they'll all have the same pivot based on their source object's pivot")]
        public bool rebaseToGrid = false;
        public bool wrapInParentObject = true;

        // for editor
        [HideInInspector] public bool populateIncludeDisabled = false;


        public void Split()
        {
            var splits = meshesToSplit
                .Select(mf => Splitter.Split(mf, gridSize, axisX, axisY, axisZ, rebaseToGrid))
                .ToList();

            if (wrapInParentObject)
            {
                Wrap(splits);
            }
        }

        static void Wrap(List<List<GameObject>> splits)
        {
            var allObjects = splits.SelectMany(go => go).ToList();

            // use center of all objects for parent's position
            var centers = allObjects.Select(go => go.transform.localPosition == Vector3.zero ? go.GetComponent<MeshFilter>().sharedMesh.bounds.center : go.transform.localPosition).ToList();
            var bounds = new Bounds(centers[0], Vector3.zero);
            foreach (var p in centers) bounds.Encapsulate(p);

            var resultGO = new GameObject("[split meshes]");
            resultGO.transform.position = bounds.center;

            foreach (var ch in allObjects)
            {
                ch.transform.SetParent(resultGO.transform, worldPositionStays: true);
            }
        }

        Bounds GlobalBounds(MeshFilter mf)
        {
            Bounds b = mf.sharedMesh.bounds;
            b.center += mf.transform.position;
            return b;
        }

        void OnDrawGizmosSelected()
        {
            if (drawGrid && meshesToSplit != null && meshesToSplit.Count > 0)
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
