#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public static class MeshSaver
{
    public static void SaveMesh(Mesh mesh, string path)
    {
#if UNITY_EDITOR
        // S’assurer que le dossier existe
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Mesh saved : " + path);
#endif
    }
}
