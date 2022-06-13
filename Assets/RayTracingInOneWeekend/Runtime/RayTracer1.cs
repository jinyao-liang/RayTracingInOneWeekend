using UnityEngine; 
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace rtwk.RayTracer1
{

public class RayTracer : IRayTracer
{
    public string desc { get => "RayTracer1: First image"; }

    public Texture2D texture { get; private set; }

    public bool isCompleted { get => texture != null; }

    public void Run()
    {
        int imageWidth = 480;
        int imageHeight = 270;
        
        var tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        var data = tex.GetRawTextureData<Color24>();

        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                var color = double3((double)x / (imageWidth - 1), (double)y / (imageHeight - 1), 0.25);
                data[y * imageWidth + x] = Color24.FromDouble3(color);
            }
        }

        tex.Apply();
        texture = tex;
    }

    public void Dispose()
    {
    }

} // class RayTracer

}

