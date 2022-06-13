using System.Collections.Generic;
using UnityEngine; 
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace rtwk.RayTracer5
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

struct HitRecord
{
    public double3 p;

    public double3 normal;

    public double t;

    public bool frontFace;

    public void SetFaceNormal(Ray r, double3 outNormal)
    {
        frontFace = dot(r.dir, outNormal) < 0;
        normal = frontFace ? outNormal : -outNormal;
    }
}

abstract class Hittable
{
    public abstract bool Hit(Ray r, double tMin, double tMax, out HitRecord rec);
}

class Sphere : Hittable
{
    public double3 center;
    public double radius;

    public Sphere(double3 c, double r)
    {
        center = c;
        radius = r;
    }

    public override bool Hit(Ray r, double tMin, double tMax, out HitRecord rec)
    {
        rec = new HitRecord();

        var oc = r.origin - center;
        var a = lengthsq(r.dir);
        var hb = dot(oc, r.dir);
        var c = lengthsq(oc) - radius * radius;
        var discriminant = hb * hb - a * c;
        if (discriminant < 0)
            return false;

        var d = sqrt(discriminant);
        var root = (-hb - d) / a;
        if (root < tMin || root >= tMax)
        {
            root = (-hb + d) / a;
            if (root < tMin || root >= tMax)
                return false;
        }

        rec.t = root;
        rec.p = r.At(rec.t);
        rec.SetFaceNormal(r, (rec.p - center) / radius);
        return true;
    }
}

class HittableList : Hittable
{
    public List<Hittable> objects = new List<Hittable>();

    public void Add(Hittable obj)
    {
        objects.Add(obj);
    }

    public override bool Hit(Ray r, double tMin, double tMax, out HitRecord rec)
    {
        rec = new HitRecord();
        var hitAny = false;
        var t = tMax;

        foreach(var obj in objects)
        {
            if (obj.Hit(r, tMin, t, out var tempRec))
            {
                hitAny = true;
                t = tempRec.t;
                rec = tempRec;
            }
        }

        return hitAny;
    }
}

public class RayTracer : IRayTracer
{
    public string desc { get => "RayTracer5: Resulting render of normals-colored sphere with ground"; }

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

        var world = new HittableList();
        world.Add(new Sphere(double3(0, 0, -1), 0.5));
        world.Add(new Sphere(double3(0, -100.5, -1), 100));

        var tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        var data = tex.GetRawTextureData<Color24>();

        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                var u = (double)x / (imageWidth - 1);
                var v = (double)y / (imageHeight - 1);
                var r = new Ray(origin, lowerLeftCorner + u * horizontal + v * vertical - origin);
                var color = RayColor(r, world);
                data[y * imageWidth + x] = Color24.FromDouble3(color);
            }
        }

        tex.Apply();
        texture = tex;
    }

    double3 RayColor(Ray ray, HittableList world)
    {
        if (world.Hit(ray, 0, double.PositiveInfinity, out var rec))
            return 0.5 * (rec.normal + double3(1, 1, 1));

        var dir = normalize(ray.dir);
        var t = 0.5 * (dir.y + 1);
        return (1.0 - t) * double3(1.0, 1.0, 1.0) + t * double3(0.5, 0.7, 1.0);
    }

    public void Dispose()
    {
    }

} // class RayTracer

}

