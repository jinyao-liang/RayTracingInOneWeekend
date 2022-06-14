using UnityEngine; 
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace rtwk.RayTracer2
{

struct Ray
{
    public double3 origin;
    public double3 dir;

    public Ray(double3 o, double3 d)
    {
        origin = o;
        dir = d;
    }

    public double3 At(double t)
    {
        return origin + t * dir;
    }
}

public class RayTracer : IRayTracer
{
    public string desc { get => "A blue-to-white gradient depending on ray Y coordinate"; }

    public Texture2D texture { get; private set; }

    public bool isCompleted { get => texture != null; }

    public void Run()
    {
        var aspectRatio = 16.0 / 9.0;
        var imageWidth = 480;
        var imageHeight = (int)(imageWidth / aspectRatio);

        var viewportHeight = 2.0;
        var viewportWidth = viewportHeight * aspectRatio;
        var focalLength = 1.0;

        var origin = new double3(0, 0, 0);
        var horizontal = new double3(viewportWidth, 0, 0);
        var vertical = new double3(0, viewportHeight, 0);
        var lowerLeftCorner = origin - horizontal / 2 - vertical / 2 - double3(0, 0, focalLength);

        var tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        var data = tex.GetRawTextureData<Color24>();

        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                var u = (double)x / (imageWidth - 1);
                var v = (double)y / (imageHeight - 1);
                var r = new Ray(origin, lowerLeftCorner + u * horizontal + v * vertical - origin);
                var color = RayColor(r);
                data[y * imageWidth + x] = Color24.FromDouble3(color);
            }
        }

        tex.Apply();
        texture = tex;
    }

    double3 RayColor(Ray ray)
    {
        var dir = normalize(ray.dir);
        var t = 0.5 * (dir.y + 1);
        return (1.0 - t) * double3(1.0, 1.0, 1.0) + t * double3(0.5, 0.7, 1.0);
    }

    public void Dispose()
    {
    }

} // class RayTracer

}

