using System;
using System.Collections.Generic;
using UnityEngine; 
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace rtwk.RayTracer9
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

class Camera
{
    double3 origin;
    double3 horizontal;
    double3 vertical;
    double3 lowerLeftCorner;

    public Camera(double aspectRatio)
    {
        var viewportHeight = 2.0;
        var viewportWidth = viewportHeight * aspectRatio;
        var focalLength = 1.0;

        origin = new double3(0, 0, 0);
        horizontal = new double3(viewportWidth, 0, 0);
        vertical = new double3(0, viewportHeight, 0);
        lowerLeftCorner = origin - horizontal / 2 - vertical / 2 - double3(0, 0, focalLength);
    }

    public Ray GetRay(double u, double v)
    {
        return new Ray(origin, lowerLeftCorner + u*horizontal + v*vertical - origin);
    }
}

public class RayTracer : IRayTracer
{
    public string desc { get => "Correct rendering of Lambertian spheres"; }

    public Texture2D texture { get; private set; }

    public bool isCompleted { get => texture != null; }

    Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)DateTime.Now.Millisecond);

    public void Run()
    {
        var aspectRatio = 16.0 / 9.0;
        var imageWidth = 480;
        var imageHeight = (int)(imageWidth / aspectRatio);

        var samplesPerPixel = 100;
        var sampleScale = 1.0 / samplesPerPixel;
        var maxDepth = 50;

        var world = new HittableList();
        world.Add(new Sphere(double3(0, 0, -1), 0.5));
        world.Add(new Sphere(double3(0, -100.5, -1), 100));

        var cam = new Camera(aspectRatio);

        var tex = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        var data = tex.GetRawTextureData<Color24>();

        for (int y = 0; y < imageHeight; y++)
        {
            for (int x = 0; x < imageWidth; x++)
            {
                var color = double3(0, 0, 0);
                for(int s = 0; s < samplesPerPixel; s++)
                {
                    var u = (double)(x + random.NextDouble()) / (imageWidth - 1);
                    var v = (double)(y + random.NextDouble()) / (imageHeight - 1);
                    color += RayColor(cam.GetRay(u, v), world, maxDepth);
                }

                color = sqrt(color * sampleScale);
                color = clamp(color, double3(0, 0, 0), double3(0.999, 0.999, 0.999));

                data[y * imageWidth + x] = Color24.FromDouble3(color);
            }
        }

        tex.Apply();
        texture = tex;
    }
    
    double3 RayColor(Ray ray, HittableList world, int depth)
    {
        if (depth <= 0)
            return double3(0, 0, 0);

        if (world.Hit(ray, 0.001, double.PositiveInfinity, out var rec))
        {
            var target = rec.p + rec.normal + RandomUnitVector();
            return 0.5 * RayColor(new Ray(rec.p, target - rec.p), world, depth - 1);
        }

        var dir = normalize(ray.dir);
        var t = 0.5 * (dir.y + 1);
        return (1.0 - t) * double3(1.0, 1.0, 1.0) + t * double3(0.5, 0.7, 1.0);
    }

    double3 RandomUnitVector()
    {
        return normalize(RandomInUnitSphere());
    }
    
    double3 RandomInUnitSphere()
    {
        while (true)
        {
            var p = random.NextDouble3(double3(-1, -1, -1), double3(1, 1, 1));
            if (lengthsq(p) <= 1)
                return p;
        }
    }

    public void Dispose()
    {
    }

} // class RayTracer

}

