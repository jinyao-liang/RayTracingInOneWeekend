using System;
using System.Collections.Generic;
using UnityEngine; 
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

namespace rtwk.RayTracer21
{

abstract class Material
{
    public abstract bool Scatter(Ray r, HitRecord rec, out double3 attenuation, out Ray scattered, ref RandomGenerator rnd);
}

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

    public Material mat;

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
    public Material mat;

    public Sphere(double3 c, double r, Material m)
    {
        center = c;
        radius = r;
        mat = m;
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

        rec.mat = mat;
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

class Lambertian : Material
{
    double3 albedo;

    public Lambertian(double3 a)
    {
        albedo = a;
    }

    public override bool Scatter(Ray r, HitRecord rec, out double3 attenuation, out Ray scattered, ref RandomGenerator rnd)
    {
        var dir = rec.normal + rnd.RandomUnitVector();
        if (lengthsq(abs(dir)) < 0.0001)
            dir = rec.normal;

        scattered = new Ray(rec.p, dir);
        attenuation = albedo;
        return true;
    }
}

class Metal : Material
{
    double3 albedo;
    double fuzz;

    public Metal(double3 a, double f)
    {
        albedo = a;
        fuzz = f;
    }

    public override bool Scatter(Ray r, HitRecord rec, out double3 attenuation, out Ray scattered, ref RandomGenerator rnd)
    {
        var reflected = reflect(normalize(r.dir), rec.normal);
        scattered = new Ray(rec.p, reflected + fuzz * rnd.RandomInUnitSphere());
        attenuation = albedo;
        return (dot(scattered.dir, rec.normal) > 0);
    }
}

class Dielectric : Material
{
    public double ir;

    public Dielectric(double i)
    {
        ir = i;
    }

    public override bool Scatter(Ray r, HitRecord rec, out double3 attenuation, out Ray scattered, ref RandomGenerator rnd)
    {
        attenuation = double3(1, 1, 1);
        var ratio = rec.frontFace ? (1.0 / ir) : ir;
        var dir = normalize(r.dir);
        var cos = min(dot(-dir, rec.normal), 1.0);
        var sin = sqrt(1 - cos * cos);

        if (ratio * sin > 1.0 || reflectance(cos, ratio) > rnd.NextDouble())
            dir = reflect(dir, rec.normal);
        else
            dir = refract(dir, rec.normal, ratio);

        scattered = new Ray(rec.p, dir);
        return true;
    }

    static double reflectance(double cos, double idx)
    {
        var r0 = (1-idx) / (1+idx);
        r0 = r0*r0;
        return r0 + (1-r0)*pow((1 - cos),5);
    }
}

struct RandomGenerator
{
    Random random;

    public RandomGenerator(uint seed)
    {
        random = new Random(seed);
    }

    public double3 RandomUnitVector()
    {
        return normalize(RandomInUnitSphere());
    }
    
    public double3 RandomInUnitSphere()
    {
        while (true)
        {
            var p = random.NextDouble3(double3(-1, -1, -1), double3(1, 1, 1));
            if (lengthsq(p) <= 1)
                return p;
        }
    }

    public double NextDouble()
    {
        return random.NextDouble();
    }

    public double3 RandomInUnitDisk()
    {
        while (true)
        {
            var p = double3(random.NextDouble(-1, 1), random.NextDouble(-1, 1), 0);
            if (lengthsq(p) < 1)
                return p;
        }
    }
}

class Camera
{
    double3 origin;
    double3 horizontal;
    double3 vertical;
    double3 lowerLeftCorner;
    double3 u;
    double3 v;
    double3 w;
    double lensRadius;

    public Camera(double3 lookfrom, double3 lookat, double3 vup, double fov, double aspectRatio,
            double aperture,
            double focusDist)
    {
        var theta = radians(fov);
        var h = tan(theta / 2); 
        var viewportHeight = 2 * h;
        var viewportWidth = viewportHeight * aspectRatio;

        w = normalize(lookfrom - lookat);
        u = cross(vup, w);
        v = cross(w, u);

        origin = lookfrom;
        horizontal = focusDist * viewportWidth * u;
        vertical = focusDist * viewportHeight * v;
        lowerLeftCorner = origin - horizontal / 2 - vertical / 2 - focusDist * w;

        lensRadius = aperture / 2;
    }

    public Ray GetRay(double s, double t, double3 randomDisk)
    {
        var rd = lensRadius * randomDisk;
        var offset = u * rd.x + v * rd.y;
        return new Ray(origin + offset, 
        lowerLeftCorner + s*horizontal + t*vertical - origin - offset);
    }
}

public class RayTracer : IRayTracer
{
    public string desc { get => "Final scene"; }

    public Texture2D texture { get; private set; }

    public bool isCompleted { get => texture != null; }

    RandomGenerator random = new RandomGenerator((uint)DateTime.Now.Millisecond);

    public void Run()
    {
        var aspectRatio = 3.0 / 2.0;
        var imageWidth = 1200;
        var imageHeight = (int)(imageWidth / aspectRatio);

        var samplesPerPixel = 500;
        var sampleScale = 1.0 / samplesPerPixel;
        var maxDepth = 50;

        var world = RandomScene();

        var lookfrom = double3(13,2,3);
        var lookat = double3(0,0,0);
        var vup = double3(0,1,0);
        var dist_to_focus = 10.0;
        var aperture = 0.1;
        var cam = new Camera(lookfrom, lookat, vup, 20, aspectRatio, aperture, dist_to_focus);

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
                    color += RayColor(cam.GetRay(u, v, random.RandomInUnitDisk()), world, maxDepth);
                }

                color = sqrt(color * sampleScale);
                color = clamp(color, double3(0, 0, 0), double3(0.999, 0.999, 0.999));

                data[y * imageWidth + x] = Color24.FromDouble3(color);
            }
        }

        tex.Apply();
        texture = tex;
    }

    HittableList RandomScene()
    {
        var world = new HittableList();

        var groundMat = new Lambertian(double3(0.5, 0.5, 0.5));
        world.Add(new Sphere(double3(0,-1000,0), 1000, groundMat));

        var rnd = new Random((uint)DateTime.Now.Millisecond);
        for (int a = -11; a < 11; a++)
        {
            for (int b = -11; b < 11; b++)
            {
                var chooseMat = rnd.NextDouble();
                var center = double3(a + 0.9*rnd.NextDouble(), 0.2, b + 0.9*rnd.NextDouble());

                if (length(center - double3(4, 0.2, 0)) > 0.9)
                {
                    Material sphereMat;

                    if (chooseMat < 0.8)
                    {
                        sphereMat = new Lambertian(rnd.NextDouble3() * rnd.NextDouble3());
                        world.Add(new Sphere(center, 0.2, sphereMat));
                    }
                    else if (chooseMat < 0.95)
                    {
                        sphereMat = new Metal(rnd.NextDouble3(double3(0.5, 0.5, 0.5), double3(1, 1, 1)), rnd.NextDouble(0, 0.5));
                        world.Add(new Sphere(center, 0.2, sphereMat));
                    }
                    else
                    {
                        sphereMat = new Dielectric(1.5);
                        world.Add(new Sphere(center, 0.2, sphereMat));
                    }
                }
            }
        }

        var material1 = new Dielectric(1.5);
        world.Add(new Sphere(double3(0, 1, 0), 1, material1));

        var material2 = new Lambertian(double3(0.4, 0.2, 0.1));
        world.Add(new Sphere(double3(-4, 1, 0), 1, material2));

        var material3 = new Metal(double3(0.7, 0.6, 0.5), 0.0);
        world.Add(new Sphere(double3(4, 1, 0), 1, material3));

        return world;
    }
    
    double3 RayColor(Ray ray, HittableList world, int depth)
    {
        if (depth <= 0)
            return double3(0, 0, 0);

        if (world.Hit(ray, 0.001, double.PositiveInfinity, out var rec))
        {
            Ray scattered;
            double3 attenuation;
            if (rec.mat.Scatter(ray, rec, out attenuation, out scattered, ref random))
                return attenuation * RayColor(scattered, world, depth - 1);
            return double3(0, 0, 0);
        }

        var dir = normalize(ray.dir);
        var t = 0.5 * (dir.y + 1);
        return (1.0 - t) * double3(1.0, 1.0, 1.0) + t * double3(0.5, 0.7, 1.0);
    }

    public void Dispose()
    {
    }

} // class RayTracer

}

