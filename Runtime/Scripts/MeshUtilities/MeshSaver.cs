#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Luzzi.PlantSystem
{

public static class MeshSaver
{
    public static void SaveMesh(Mesh mesh, string path)
    {
        SaveMesh(mesh, path, null);
    }
    
    public static void SaveMesh(Mesh mesh, string path, GameObject prefabContext)
    {
        Debug.Log("[MeshSaver] Saving mesh asset...");
#if UNITY_EDITOR
        // S'assurer que le dossier existe
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        // Vérifier si l'asset existe et stocker son GUID
        string oldGuid = null;
        Mesh oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (oldMesh != null)
        {
            oldGuid = AssetDatabase.AssetPathToGUID(path);
            
            // Collecter les MeshFilter qui référencent l'ancien mesh AVANT suppression
            if (prefabContext != null)
            {
                CollectAndReassignReferences(prefabContext, oldMesh, mesh);
            }
            
            AssetDatabase.DeleteAsset(path);
        }

        // Créer le nouveau asset
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
#endif
    }
    
#if UNITY_EDITOR
    private static void CollectAndReassignReferences(GameObject prefabRoot, Mesh oldMesh, Mesh newMesh)
    {
        int reassignedCount = 0;
        
        // Chercher le MeshCombiner sur l'objet racine (parent)
        MeshCombiner meshCombiner = prefabRoot.GetComponent<MeshCombiner>();
        
        if (meshCombiner != null)
        {
            MeshFilter rootMeshFilter = prefabRoot.GetComponent<MeshFilter>();
            
            if (rootMeshFilter != null)
            {
                if (rootMeshFilter.sharedMesh == oldMesh)
                {
                    rootMeshFilter.sharedMesh = newMesh;
                    reassignedCount++;
                }
            }
        }
    }
#endif
}

}
