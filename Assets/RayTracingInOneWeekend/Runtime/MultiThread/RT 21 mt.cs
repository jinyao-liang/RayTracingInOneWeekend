using System;
using System.Collections.Generic;
using UnityEngine; 
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

namespace rtwk.MultiThread.RayTracer21
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

enum MaterialType
{
    Lambertian,
    Metal,
    Dielectric,
}

struct Material
{
    public MaterialType type;
    public double3 albedo;
    public double fuzz;
    public double ir;
}

struct HitRecord
{
    public double3 p;

    public double3 normal;

    public double t;

    public bool frontFace;

    public Material mat;

    public void SetFaceNormal(Ray r, double3 outNormal)
    {
        frontFace = dot(r.dir, outNormal) < 0;
        normal = frontFace ? outNormal : -outNormal;
    }
}

struct Sphere
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

    public bool Hit(Ray r, double tMin, double tMax, out HitRecord rec)
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

struct HittableList
{
    NativeArray<Sphere> objects;

    public HittableList(List<Sphere> list)
    {
        objects = new NativeArray<Sphere>(list.Count, Allocator.Persistent);
        for(int i = 0; i < list.Count; i++)
        {
            objects[i] = list[i];
        }
    }

    public void Dispose()
    {
        objects.Dispose();
    }

    public bool Hit(Ray r, double tMin, double tMax, out HitRecord rec)
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

struct Camera
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

    public bool isCompleted { get => rayColorJobHandle.IsCompleted; }

    HittableList world;
    NativeArray<Random> randomArray;
    JobHandle rayColorJobHandle;

    public void Run()
    {
        var aspectRatio = 3.0 / 2.0;
        var imageWidth = 1200;
        var imageHeight = (int)(imageWidth / aspectRatio);

        var samplesPerPixel = 500;
        var sampleScale = 1.0 / samplesPerPixel;
        var maxDepth = 50;

        world = RandomScene();

        var lookfrom = double3(13,2,3);
        var lookat = double3(0,0,0);
        var vup = double3(0,1,0);
        var dist_to_focus = 10.0;
        var aperture = 0.1;
        var cam = new Camera(lookfrom, lookat, vup, 20, aspectRatio, aperture, dist_to_focus);

        texture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false, false);
        var textureData = texture.GetRawTextureData<Color24>();

        var rnd = new Random((uint)DateTime.Now.Millisecond);
        randomArray = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent);
        for (int i = 0; i < randomArray.Length; i++)
        {
            randomArray[i] = new Random((uint)rnd.NextInt());
        }

        var job = new RayColorJob
        {
            randomArray = randomArray,
            imageWidth = imageWidth,
            imageHeight = imageHeight,
            samples = samplesPerPixel,
            depth = maxDepth,
            cam = cam,
            world = world,
            pixels = textureData
        };
        rayColorJobHandle = job.Schedule(imageWidth * imageHeight, 64);
    }

    HittableList RandomScene()
    {
        var list = new List<Sphere>();

        var groundMat = new Material { type = MaterialType.Lambertian, albedo = double3(0.5, 0.5, 0.5) };
        list.Add(new Sphere(double3(0,-1000,0), 1000, groundMat));

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
                        sphereMat = new Material {
                            type = MaterialType.Lambertian,
                            albedo = rnd.NextDouble3() * rnd.NextDouble3(),
                        };
                        list.Add(new Sphere(center, 0.2, sphereMat));
                    }
                    else if (chooseMat < 0.95)
                    {
                        sphereMat = new Material {
                            type = MaterialType.Metal,
                            albedo = rnd.NextDouble3(double3(0.5, 0.5, 0.5), double3(1, 1, 1)),
                            fuzz = rnd.NextDouble(0, 0.5),
                        };
                        list.Add(new Sphere(center, 0.2, sphereMat));
                    }
                    else
                    {
                        sphereMat = new Material {
                            type = MaterialType.Dielectric,
                            ir = 1.5,
                        };
                        list.Add(new Sphere(center, 0.2, sphereMat));
                    }
                }
            }
        }

        var material1 = new Material {
            type = MaterialType.Dielectric,
            ir = 1.5,
        };
        list.Add(new Sphere(double3(0, 1, 0), 1, material1));

        var material2 = new Material {
            type = MaterialType.Lambertian,
            albedo = double3(0.4, 0.2, 0.1),
        };
        list.Add(new Sphere(double3(-4, 1, 0), 1, material2));

        var material3 = new Material {
            type = MaterialType.Metal,
            albedo = double3(0.7, 0.6, 0.5),
            fuzz = 0,
        };
        list.Add(new Sphere(double3(4, 1, 0), 1, material3));

        return new HittableList(list);
    }
    
    [BurstCompile(CompileSynchronously = true)]
    struct RayColorJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<Random> randomArray;

        public int imageWidth;
        public int imageHeight;
        public int samples;
        public int depth;
        public Camera cam;

        [ReadOnly]
        public HittableList world;

        [WriteOnly]
        public NativeArray<Color24> pixels;

        [NativeSetThreadIndex]
        private int nativeThreadIndex;

        public void Execute(int i)
        {
            var x = i % imageWidth;
            var y = (i / imageWidth);
            var sampleScale = 1.0 / samples;

            var color = double3(0, 0, 0);
            for(int s = 0; s < samples; s++)
            {
                var u = (double)(x + NextDouble()) / (imageWidth - 1);
                var v = (double)(y + NextDouble()) / (imageHeight - 1);
                color += RayColor(cam.GetRay(u, v, RandomInUnitDisk()), world, depth);
            }

            color = sqrt(color * sampleScale);
            color = clamp(color, double3(0, 0, 0), double3(0.999, 0.999, 0.999));

            pixels[i] = Color24.FromDouble3(color);
        }

        double3 RayColor(Ray ray, HittableList world, int depth)
        {
            if (depth <= 0)
                return double3(0, 0, 0);

            if (world.Hit(ray, 0.001, double.PositiveInfinity, out var rec))
            {
                Ray scattered;
                double3 attenuation;
                if (Scatter(rec.mat, ray, rec, out attenuation, out scattered))
                {
                    return attenuation * RayColor(scattered, world, depth - 1);
                }
                return double3(0, 0, 0);
            }

            var dir = normalize(ray.dir);
            var t = 0.5 * (dir.y + 1);
            return (1.0 - t) * double3(1.0, 1.0, 1.0) + t * double3(0.5, 0.7, 1.0);
        }

        public bool Scatter(Material mat, Ray r, HitRecord rec, out double3 attenuation, out Ray scattered)
        {
            switch (mat.type)
            {
            case MaterialType.Metal:
                 return MetalScatter(mat, r, rec, out attenuation, out scattered);
            case MaterialType.Dielectric:
                 return DielectricScatter(mat, r, rec, out attenuation, out scattered);
            }

            return LambertianScatter(mat, r, rec, out attenuation, out scattered);
        }

        bool LambertianScatter(Material mat, Ray r, HitRecord rec, out double3 attenuation, out Ray scattered)
        {
            var dir = rec.normal + RandomUnitVector();
            if (lengthsq(abs(dir)) < 0.0001)
                dir = rec.normal;

            scattered = new Ray(rec.p, dir);
            attenuation = mat.albedo;
            return true;
        }

        bool MetalScatter(Material mat, Ray r, HitRecord rec, out double3 attenuation, out Ray scattered)
        {
            var reflected = reflect(normalize(r.dir), rec.normal);
            scattered = new Ray(rec.p, reflected + mat.fuzz * RandomInUnitSphere());
            attenuation = mat.albedo;
            return (dot(scattered.dir, rec.normal) > 0);
        }
        
        bool DielectricScatter(Material mat, Ray r, HitRecord rec, out double3 attenuation, out Ray scattered)
        {
            attenuation = double3(1, 1, 1);
            var ratio = rec.frontFace ? (1.0 / mat.ir) : mat.ir;
            var dir = normalize(r.dir);
            var cos = min(dot(-dir, rec.normal), 1.0);
            var sin = sqrt(1 - cos * cos);

            if (ratio * sin > 1.0 || reflectance(cos, ratio) > NextDouble())
                dir = reflect(dir, rec.normal);
            else
                dir = refract(dir, rec.normal, ratio);

            scattered = new Ray(rec.p, dir);
            return true;
        }

        double reflectance(double cos, double idx) 
        {
            var r0 = (1-idx) / (1+idx);
            r0 = r0*r0;
            return r0 + (1-r0)*pow((1 - cos),5);
        }

        double NextDouble()
        {
            var rnd = randomArray[nativeThreadIndex];
            var v = rnd.NextDouble();
            randomArray[nativeThreadIndex] = rnd;
            return v;
        }

        double NextDouble(double min, double max)
        {
            var rnd = randomArray[nativeThreadIndex];
            var v = rnd.NextDouble(min, max);
            randomArray[nativeThreadIndex] = rnd;
            return v;
        }

        double3 NextDouble3(double3 min, double3 max)
        {
            var rnd = randomArray[nativeThreadIndex];
            var v = rnd.NextDouble3(min, max);
            randomArray[nativeThreadIndex] = rnd;
            return v;
        }
    
        double3 RandomInUnitSphere()
        {
            while (true)
            {
                var p = NextDouble3(double3(-1, -1, -1), double3(1, 1, 1));
                if (lengthsq(p) <= 1)
                    return p;
            }
        }
        
        double3 RandomUnitVector()
        {
            return normalize(RandomInUnitSphere());
        }

        double3 RandomInUnitDisk()
        {
            while (true)
            {
                var p = double3(NextDouble(-1, 1), NextDouble(-1, 1), 0);
                if (lengthsq(p) < 1)
                    return p;
            }
        }

    }

    public void Dispose()
    {
        rayColorJobHandle.Complete();
        world.Dispose();
        randomArray.Dispose();
    }

} // class RayTracer

}

