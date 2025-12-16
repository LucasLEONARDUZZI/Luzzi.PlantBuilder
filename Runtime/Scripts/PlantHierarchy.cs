using System.Collections.Generic;
using UnityEngine;

namespace Luzzi.PlantBuilder{
[ExecuteInEditMode]
public abstract class PlantHierarchy : MonoBehaviour
{
    protected List<PlantNode> _nodes = new List<PlantNode>();

    protected virtual void UpdateHierarchy(PlantHierarchy self, ComputeShader computeShader)
    {
        int childrenCount = self.gameObject.transform.childCount;
        if (childrenCount == 0) return;

        _nodes.Clear();

        for (int childIndex = childrenCount - 1; childIndex >= 0; childIndex--)
        {
            GameObject child = self.gameObject.transform.GetChild(childIndex).gameObject;
            if (child != null)
            {
                PlantNode childNode = child.GetComponent<PlantNode>();
                if (childNode == null)
                {
                    MeshFilter childMeshFilter = child.GetComponent<MeshFilter>();
                    if (childMeshFilter != null && childMeshFilter.sharedMesh != null)
                    {
                        childNode = child.AddComponent<PlantNode>();
                    }
                }

                childNode.Setup(computeShader);
                self._nodes.Add(childNode);
            }
        }

        for (int childIndex = 0; childIndex < self._nodes.Count; childIndex++)
        {
            PlantNode node = self._nodes[childIndex];
            node?.UpdateHierarchy(node, computeShader);
            node.gameObject.name = self.gameObject.name + "_" + childIndex;
        }
    }
}
}
