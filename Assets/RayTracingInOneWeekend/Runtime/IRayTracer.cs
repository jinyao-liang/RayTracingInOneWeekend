using UnityEngine;
using Unity.Mathematics;

namespace rtwk
{

public interface IRayTracer
{
    string desc { get; }
    Texture2D texture { get; }
    bool isCompleted { get; }

    void Run();
    void Dispose();
} // interface IRayTracer

public struct Color24
{
    public byte r;
    public byte g;
    public byte b;

    public Color24(byte red, byte green, byte blue)
    {
        r = red;
        g = green;
        b = blue;
    }

    public static Color24 FromDouble3(double3 color)
    {
        return new Color24((byte)(255.999 * color.x), (byte)(255.999 * color.y), (byte)(255.999 * color.z));
    }

} // struct Color24

} // namespace rtwk