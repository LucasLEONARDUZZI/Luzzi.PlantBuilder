using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GlobalTextureDefaultType
{
    None,
    Black,
    Grey,
    White,
    Noise,
}

[Serializable]
public struct GlobalTextureDefaultSettings
{
    public string reference;
    public GlobalTextureDefaultType type;
}

// This class allows to set some not yet created global textures to avoid messing up the shaders in EditMode
[ExecuteAlways]
public class GlobalTexturesDefault : MonoBehaviour
{
    [SerializeField]
    private Texture2D _noiseTexture = null;

    [SerializeField]
    private List<GlobalTextureDefaultSettings> _settings = new List<GlobalTextureDefaultSettings>();
    private void OnEnable()
    {
        UpdateGlobalTexturesDefault();
    }
    private void OnValidate()
    {
        UpdateGlobalTexturesDefault();
    }
    private void UpdateGlobalTexturesDefault()
    {
        if (Application.isPlaying) return;

        foreach (GlobalTextureDefaultSettings setting in _settings)
        {
            string reference = setting.reference;
            Texture2D defaultTexture;
            switch (setting.type)
            {
                case GlobalTextureDefaultType.Black:
                    defaultTexture = Texture2D.blackTexture;
                    break;
                case GlobalTextureDefaultType.Grey:
                    defaultTexture = Texture2D.grayTexture;
                    break;
                case GlobalTextureDefaultType.White:
                    defaultTexture = Texture2D.whiteTexture;
                    break;
                case GlobalTextureDefaultType.Noise:
                    defaultTexture = _noiseTexture ? _noiseTexture : Texture2D.blackTexture;
                    break;
                default:
                    defaultTexture = Texture2D.blackTexture;
                    break;
            }
            Shader.SetGlobalTexture(reference, defaultTexture);
        }
    }
}
