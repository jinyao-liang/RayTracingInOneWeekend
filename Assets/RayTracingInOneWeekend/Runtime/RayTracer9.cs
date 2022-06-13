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

interface Hittable
{
    bool Hit(Ray r, double tMin, double tMax, out HitRecord rec);
}

struct Sphere : Hittable
{
    public double3 center;
    public double radius;

    public Sphere(double3 c, double r)
    {
        center = c;
        radius = r;
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

        rec.t = root;
        rec.p = r.At(rec.t);
        rec.SetFaceNormal(r, (rec.p - center) / radius);
        return true;
    }
}

struct HittableList : Hittable
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
    public string desc { get => "RayTracer9: Correct rendering of Lambertian spheres"; }

    public Texture2D texture { get; private set; }

    public bool isCompleted { get => rayColorJobHandle.IsCompleted; }

    HittableList world;
    JobHandle rayColorJobHandle;

    public void Run()
    {
        var aspectRatio = 16.0 / 9.0;
        var imageWidth = 480;
        var imageHeight = (int)(imageWidth / aspectRatio);

        var samplesPerPixel = 100;
        var sampleScale = 1.0 / samplesPerPixel;
        var maxDepth = 50;

        var viewportHeight = 2.0;
        var viewportWidth = viewportHeight * aspectRatio;
        var focalLength = 1.0;

        var origin = new double3(0, 0, 0);
        var horizontal = new double3(viewportWidth, 0, 0);
        var vertical = new double3(0, viewportHeight, 0);
        var lowerLeftCorner = origin - horizontal / 2 - vertical / 2 - double3(0, 0, focalLength);

        world = new HittableList(new List<Sphere> {
            new Sphere(double3(0, 0, -1), 0.5),
            new Sphere(double3(0, -100.5, -1), 100),
        });

        var cam = new Camera(aspectRatio);

        texture = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false, false);
        var textureData = texture.GetRawTextureData<Color24>();

        var rnd = new Random((uint)DateTime.Now.Millisecond);
        var randoms = new NativeArray<Random>(JobsUtility.MaxJobThreadCount, Allocator.Persistent);
        for (int i = 0; i < randoms.Length; i++)
        {
            randoms[i] = new Random((uint)rnd.NextInt());
        }

        var job = new RayColorJob
        {
            imageWidth = imageWidth,
            imageHeight = imageHeight,
            samples = samplesPerPixel,
            depth = maxDepth,
            cam = cam,
            world = world,
            randoms = randoms,
            pixels = textureData
        };
        rayColorJobHandle = job.Schedule(imageWidth * imageHeight, 32);
    }
    
    [BurstCompile(CompileSynchronously = true)]
    struct RayColorJob : IJobParallelFor
    {
        public int imageWidth;
        public int imageHeight;
        public int samples;
        public int depth;
        public Camera cam;

        [NativeSetThreadIndex]
        private int tid;
        
        [NativeDisableParallelForRestriction]
        [DeallocateOnJobCompletion]
        public NativeArray<Random> randoms;

        [ReadOnly]
        public HittableList world;

        [WriteOnly]
        public NativeArray<Color24> pixels;

        public void Execute(int i)
        {
            var x = i % imageWidth;
            var y = (i / imageWidth);
            var sampleScale = 1.0 / samples;

            var color = double3(0, 0, 0);
            for(int s = 0; s < samples; s++)
            {
                var u = (double)(x + randoms[tid].NextDouble()) / (imageWidth - 1);
                var v = (double)(y + randoms[tid].NextDouble()) / (imageHeight - 1);
                color += RayColor(cam.GetRay(u, v), world, depth);
            }

            color *= sampleScale;
            color = sqrt(color);
            color = clamp(color, double3(0, 0, 0), double3(0.999, 0.999, 0.999));

            pixels[i] = Color24.FromDouble3(color);
        }

        double3 RayColor(Ray ray, HittableList world, int depth)
        {
            if (depth <= 0)
                return double3(0, 0, 0);

            if (world.Hit(ray, 0, double.PositiveInfinity, out var rec))
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
                var p = randoms[tid].NextDouble3(double3(-1, -1, -1), double3(1, 1, 1));
                if (lengthsq(p) <= 1)
                    return p;
            }
        }
    }

    public void Dispose()
    {
        rayColorJobHandle.Complete();
        world.Dispose();
    }

} // class RayTracer

}

