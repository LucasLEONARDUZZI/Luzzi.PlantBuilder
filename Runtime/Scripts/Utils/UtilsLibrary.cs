using UnityEngine;

public static class UtilsLibrary
{
    public static RenderTexture CreateRT(int width, int height, RenderTextureFormat format, FilterMode filter = FilterMode.Bilinear, bool useMipMap = false)
    {
        RenderTexture renderTexture = new RenderTexture(width, height, 0, format)
        {
            enableRandomWrite = true,
            autoGenerateMips = false,
            useMipMap = useMipMap,
            filterMode = filter, // not really used, actually we filter manually in the shader for performance issues
            wrapMode = TextureWrapMode.Clamp
        };

        renderTexture.Create();
        return renderTexture;
    }

    public static void ClearRT(RenderTexture renderTexture)
    {
        // Store current active render texture
        RenderTexture currentRenderTexture = RenderTexture.active;
        // Make the clear
        RenderTexture.active = renderTexture;
        GL.Clear(false, true, Color.clear);
        // Bring back the current active render texture (as if nothing happened !)
        RenderTexture.active = currentRenderTexture;
    }

    public static void SwapRT(ref RenderTexture a, ref RenderTexture b)
    {
        // Swap render texture reference (to make a ping-pong pattern)
        RenderTexture temp = a;
        a = b;
        b = temp;
    }

    public static bool EnsureCreatedRT(RenderTexture renderTexture)
    {
        if(renderTexture == null || !renderTexture.IsCreated()) return false;
        return true;
    }


    public static void Dispatch(ComputeShader computeShader, Vector2Int size, int kernel)
    {
        // we divide width and height by 8 to match [numthreads(8, 8, 1)] in the compute shader
        int x = Mathf.CeilToInt(size.x / 8f);
        int y = Mathf.CeilToInt(size.y / 8f);
        computeShader.Dispatch(kernel, x, y, 1);
    }
}
