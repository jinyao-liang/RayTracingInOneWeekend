using System;
using System.Collections;
using System.IO;
using Unity.EditorCoroutines.Editor;
using UnityEngine;
using UnityEditor;  
using Stopwatch = System.Diagnostics.Stopwatch;

namespace rtwk
{

public class MyEditorWindow : EditorWindow
{
    static Type[] rayTracerList = new Type[] {
        typeof(rtwk.RayTracer1.RayTracer),
        typeof(rtwk.RayTracer2.RayTracer),
        typeof(rtwk.RayTracer3.RayTracer),
        typeof(rtwk.RayTracer4.RayTracer),
        typeof(rtwk.RayTracer5.RayTracer),
        typeof(rtwk.RayTracer6.RayTracer),
        typeof(rtwk.RayTracer7.RayTracer),
    };
    
    static Type[] rayTracerPairList = new Type[] {
        typeof(rtwk.RayTracer8.RayTracer),
        typeof(rtwk.MultiThread.RayTracer8.RayTracer),
        typeof(rtwk.RayTracer9.RayTracer),
        typeof(rtwk.MultiThread.RayTracer9.RayTracer),
        typeof(rtwk.RayTracer10.RayTracer),
        typeof(rtwk.MultiThread.RayTracer10.RayTracer),
        typeof(rtwk.RayTracer11.RayTracer),
        typeof(rtwk.MultiThread.RayTracer11.RayTracer),
        typeof(rtwk.RayTracer12.RayTracer),
        typeof(rtwk.MultiThread.RayTracer12.RayTracer),
        typeof(rtwk.RayTracer14.RayTracer),
        typeof(rtwk.MultiThread.RayTracer14.RayTracer),
        typeof(rtwk.RayTracer15.RayTracer),
        typeof(rtwk.MultiThread.RayTracer15.RayTracer),
        typeof(rtwk.RayTracer16.RayTracer),
        typeof(rtwk.MultiThread.RayTracer16.RayTracer),
        typeof(rtwk.RayTracer17.RayTracer),
        typeof(rtwk.MultiThread.RayTracer17.RayTracer),
        typeof(rtwk.RayTracer18.RayTracer),
        typeof(rtwk.MultiThread.RayTracer18.RayTracer),
        typeof(rtwk.RayTracer19.RayTracer),
        typeof(rtwk.MultiThread.RayTracer19.RayTracer),
        typeof(rtwk.RayTracer20.RayTracer),
        typeof(rtwk.MultiThread.RayTracer20.RayTracer),
        typeof(rtwk.RayTracer21.RayTracer),
        typeof(rtwk.MultiThread.RayTracer21.RayTracer),
    };

    string rayTracerDesc;
    Texture2D previewTexture;
    Stopwatch stopwatch;

    EditorWaitForSeconds waitForOneSecond = new EditorWaitForSeconds(1.0f);

    [MenuItem ("rtwk/Show Window")]
    public static void  ShowWindow () {
        EditorWindow.GetWindow(typeof(MyEditorWindow), false, "Ray Tracing in One Weekend");
    }

    string GetRayTracerTypeName(Type t)
    {
        return t.FullName.Substring(5, t.FullName.Length-15);
    }

    void OnGUI () {
        EditorGUILayout.Space();

        GUI.enabled = stopwatch == null;
        for(int i = 0; i < rayTracerList.Length; i++)
        {
            if(GUILayout.Button($"{GetRayTracerTypeName(rayTracerList[i])}"))
            {
                var rayTracer = (IRayTracer)Activator.CreateInstance(rayTracerList[i]);
                EditorCoroutineUtility.StartCoroutine(RunRayTracer(rayTracer), this);
            }
        }
        for(int i = 0; i < rayTracerPairList.Length; i+=2)
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button($"{GetRayTracerTypeName(rayTracerPairList[i])}"))
            {
                var rayTracer = (IRayTracer)Activator.CreateInstance(rayTracerPairList[i]);
                EditorCoroutineUtility.StartCoroutine(RunRayTracer(rayTracer), this);
            }
            if (i+1 < rayTracerPairList.Length)
            {
                if(GUILayout.Button($"{GetRayTracerTypeName(rayTracerPairList[i+1])}"))
                {
                    var rayTracer = (IRayTracer)Activator.CreateInstance(rayTracerPairList[i+1]);
                    EditorCoroutineUtility.StartCoroutine(RunRayTracer(rayTracer), this);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(rayTracerDesc);
        if (stopwatch != null)
        {
            EditorGUILayout.LabelField($"running: {stopwatch.Elapsed.TotalSeconds:F2} seconds...");
        }
        if (previewTexture != null)
        {
            var ratio = (float)previewTexture.height / previewTexture.width;
            Vector2 sz = new Vector2(EditorGUIUtility.currentViewWidth, EditorGUIUtility.currentViewWidth * ratio);
            Rect r = EditorGUILayout.GetControlRect(false, GUILayout.Height(sz.y), GUILayout.ExpandHeight(false));
            EditorGUI.DrawPreviewTexture(r, previewTexture);

            if(GUILayout.Button("Save"))
            {
                var path = EditorUtility.SaveFilePanel(
                    "Save texture as PNG",
                    "",
                    "image",
                    "png");
                if (path.Length != 0)
                {
                    File.WriteAllBytes(path, previewTexture.EncodeToPNG());
                }
            }
        }
    }

    public IEnumerator RunRayTracer(IRayTracer rayTracer)
    {
        rayTracerDesc = $"{GetRayTracerTypeName(rayTracer.GetType())}: {rayTracer.desc}";
        previewTexture = null;

        stopwatch = new Stopwatch();
        stopwatch.Start();

        rayTracer.Run();
        while (!rayTracer.isCompleted)
        {
            yield return waitForOneSecond;
            Repaint();
        }

        stopwatch.Stop();
        Debug.Log($"{GetRayTracerTypeName(rayTracer.GetType())} finish running in {stopwatch.Elapsed.TotalSeconds:F2} seconds.");

        previewTexture = rayTracer.texture;
        previewTexture.Apply();
        Repaint();

        rayTracer.Dispose();
        stopwatch = null;
    }
} // class MyEditorWindow

} // namespace rtwk
