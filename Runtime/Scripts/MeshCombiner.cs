using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Luzzi.PlantSystem
{
    [ExecuteInEditMode, RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
    public class MeshCombiner : MonoBehaviour
    {
        private MeshFilter _meshFilter => GetComponent<MeshFilter>();
        private bool _isCombined = false;
        public bool isCombined => _isCombined;
        GameObject[] _combineObjects;
        [SerializeField]
        private string _saveDirectory = "Assets/Tools/PlantBuilder/Meshes/";

        public void Combine(Dictionary<GameObject, Mesh> objectsMeshes)
        {
            if (_meshFilter == null) return;

            _combineObjects = objectsMeshes.Keys.ToArray();
            Mesh[] meshes = objectsMeshes.Values.ToArray();
            CombineInstance[] combine = new CombineInstance[objectsMeshes.Count];
            for (int i = 0; i < combine.Length; i++)
            {
                Mesh cloneMesh = Instantiate(meshes[i]);

                combine[i].mesh = cloneMesh;
                combine[i].transform = gameObject.transform.worldToLocalMatrix * _combineObjects[i].transform.localToWorldMatrix;

                _combineObjects[i].gameObject.SetActive(false);
            }

            Mesh combinedMesh = new Mesh();
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combine);

            _meshFilter.sharedMesh = combinedMesh;
            _isCombined = true;
        }

        public void UnCombine()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                if (children[index] != null)
                {
                    children[index].gameObject.SetActive(true);
                }
            }

            _meshFilter.mesh = null;

            _isCombined = false;
        }

        public void NormalizeUVs(int channel, int axis)
        {
            if (_meshFilter == null) return;

            Mesh mesh = _meshFilter.sharedMesh;
            if (mesh == null) return;

            List<Vector4> uvs = new List<Vector4>();
            mesh.GetUVs(channel, uvs);

            // Find max and min value in UV
            float maxValue = float.MinValue;
            float minValue = float.MaxValue;
            for (int index = 0; index < uvs.Count; index++)
            {
                float currentValue = 0f;
                switch (axis)
                {
                    case 0: currentValue = uvs[index].x; break;
                    case 1: currentValue = uvs[index].y; break;
                    case 2: currentValue = uvs[index].z; break;
                    case 3: currentValue = uvs[index].w; break;
                }

                if (currentValue < minValue)
                {
                    minValue = currentValue;
                }

                if (currentValue > maxValue)
                {
                    maxValue = currentValue;
                }
            }

            // offset and scale
            for (int index = 0; index < uvs.Count; index++)
            {
                float currentValue = 0f;
                switch (axis)
                {
                    case 0: currentValue = uvs[index].x; break;
                    case 1: currentValue = uvs[index].y; break;
                    case 2: currentValue = uvs[index].z; break;
                    case 3: currentValue = uvs[index].w; break;
                }

                // offsets to snap on bottom of axis
                currentValue -= minValue;

                float range = maxValue - minValue + float.Epsilon;
                currentValue /= range;

                switch (axis)
                {
                    case 0: uvs[index] = new Vector4(currentValue, uvs[index].y, uvs[index].z, uvs[index].w); break;
                    case 1: uvs[index] = new Vector4(uvs[index].x, currentValue, uvs[index].z, uvs[index].w); break;
                    case 2: uvs[index] = new Vector4(uvs[index].x, uvs[index].y, currentValue, uvs[index].w); break;
                    case 3: uvs[index] = new Vector4(uvs[index].x, uvs[index].y, uvs[index].z, currentValue); break;
                }
            }

            mesh.SetUVs(channel, uvs.ToArray());
        }

        public void SetUVsConstant(int channel, int axis, float value)
        {
            if (_meshFilter == null) return;

            Mesh mesh = _meshFilter.sharedMesh;
            if (mesh == null) return;

            int vertexCount = mesh.vertexCount;
            if (vertexCount == 0) return;

            List<Vector4> uvs = new List<Vector4>();
            mesh.GetUVs(channel, uvs);

            if(uvs.Count == 0)
            {
                Vector4[] uvsArray = new Vector4[vertexCount];
                uvs = uvsArray.ToList();
            }
            
            for (int index = 0; index < uvs.Count; index++)
            {
                switch (axis)
                {
                    case 0: uvs[index] = new Vector4(value, uvs[index].y, uvs[index].z, uvs[index].w); break;
                    case 1: uvs[index] = new Vector4(uvs[index].x, value, uvs[index].z, uvs[index].w); break;
                    case 2: uvs[index] = new Vector4(uvs[index].x, uvs[index].y, value, uvs[index].w); break;
                    case 3:
                        uvs[index] = new Vector4(uvs[index].x, uvs[index].y, uvs[index].z, value);
                        break;
                }
            }

            mesh.SetUVs(channel, uvs.ToArray());
        }

        public void SaveMesh()
        {
            SaveMesh(false); // Default: assume new mesh for backward compatibility
        }
        
        public void SaveMesh(bool wasExistingMesh)
        {
#if UNITY_EDITOR
            if (_meshFilter == null) return;
            Mesh mesh = _meshFilter.sharedMesh;

            if (mesh == null) return;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            string assetPath = null;
            GameObject prefabAsset = null;

            if (prefabStage != null)
            {
                // We are in the prefab stage
                assetPath = prefabStage.assetPath;
                prefabAsset = prefabStage.prefabContentsRoot;
            }
            else
            {
                prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (prefabAsset != null)
                {
                    assetPath = AssetDatabase.GetAssetPath(prefabAsset);
                }
                else
                {
                    // fallback
                    assetPath = AssetDatabase.GetAssetPath(gameObject);
                    prefabAsset = gameObject;
                }
            }
            string prefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
            string prefabShortGuid = prefabGuid.Length > 8 ? prefabGuid.Substring(0, 8) : prefabGuid;

            mesh.name = $"{prefabAsset.name}_combinedMesh_{prefabShortGuid}";
            string path = $"{_saveDirectory}{mesh.name}.asset";

            // Use MeshSaver if available, otherwise skip saving in runtime
            var meshSaverType = System.Type.GetType("Luzzi.PlantSystem.Editor.MeshSaver");
            if (meshSaverType != null)
            {
                var saveMethod = meshSaverType.GetMethod("SaveMesh", new System.Type[] { typeof(Mesh), typeof(string), typeof(GameObject) });
                if (saveMethod != null)
                {
                    saveMethod.Invoke(null, new object[] { mesh, path, gameObject });
                }
            }
            
            // Réassigner le mesh sauvé au MeshFilter pour éviter les références perdues
            Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (savedMesh != null && _meshFilter != null)
            {            
                _meshFilter.sharedMesh = savedMesh;
                
                // CRITICAL: Pour les nouveaux assets, forcer la persistence
                if (prefabStage != null)
                {
                    if (!wasExistingMesh)
                    {
                        // NOUVEAU MESH : Forcer la sauvegarde immédiate du prefab
                        Debug.Log($"MeshCombiner: NEW MESH detected, forcing prefab save");
                        EditorUtility.SetDirty(_meshFilter);
                        EditorUtility.SetDirty(gameObject);
                        
                        // Pour les prefab stages, utiliser l'API spécifique
                        try 
                        {
                            // Sauvegarder le prefab avec les nouvelles références
                            PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
                            Debug.Log($"MeshCombiner: Saved prefab asset with new mesh reference");
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"MeshCombiner: Could not save prefab asset: {e.Message}");
                            // Fallback simple
                            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                        }
                    }
                    else
                    {
                        // MESH EXISTANT : Marquer comme dirty suffit
                        EditorUtility.SetDirty(gameObject);
                    }
                }
            }
            else
            {
                // Fallback pour objets hors prefab
                EditorUtility.SetDirty(gameObject);
            }
#else
            // Runtime: Just keep the mesh in memory without saving to asset
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
                Debug.Log("MeshCombiner: Runtime mesh combination completed (not saved to assets)");
            }
#endif
        }
    }
}