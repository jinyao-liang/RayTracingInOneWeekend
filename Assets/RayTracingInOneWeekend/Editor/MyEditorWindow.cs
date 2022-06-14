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
        typeof(rtwk.RayTracer8mt.RayTracer),
        typeof(rtwk.RayTracer9.RayTracer),
        typeof(rtwk.RayTracer9mt.RayTracer),
        typeof(rtwk.RayTracer10.RayTracer),
        typeof(rtwk.RayTracer10mt.RayTracer),
        typeof(rtwk.RayTracer11.RayTracer),
        typeof(rtwk.RayTracer11mt.RayTracer),
        typeof(rtwk.RayTracer12.RayTracer),
        typeof(rtwk.RayTracer12mt.RayTracer),
    };

    IRayTracer activeRayTracer;
    string previewDesc;
    Texture2D previewTexture;

    EditorWaitForSeconds waitForOneSecond = new EditorWaitForSeconds(1.0f);

    [MenuItem ("Window/rtwk")]
    public static void  ShowWindow () {
        EditorWindow.GetWindow(typeof(MyEditorWindow));
    }

    string GetRayTracerTypeName(Type t)
    {
        return t.FullName.Substring(5, t.FullName.Length-15);
    }

    void OnGUI () {
        EditorGUILayout.Space();

        GUI.enabled = activeRayTracer == null;
        for(int i = 0; i < rayTracerList.Length; i++)
        {
            if(GUILayout.Button($"Run {GetRayTracerTypeName(rayTracerList[i])}"))
            {
                var rayTracer = (IRayTracer)Activator.CreateInstance(rayTracerList[i]);
                EditorCoroutineUtility.StartCoroutine(RunRayTracer(rayTracer), this);
            }
        }
        for(int i = 0; i < rayTracerPairList.Length; i+=2)
        {
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button($"Run {GetRayTracerTypeName(rayTracerPairList[i])}"))
            {
                var rayTracer = (IRayTracer)Activator.CreateInstance(rayTracerPairList[i]);
                EditorCoroutineUtility.StartCoroutine(RunRayTracer(rayTracer), this);
            }
            if (i+1 < rayTracerPairList.Length)
            {
                if(GUILayout.Button($"Run {GetRayTracerTypeName(rayTracerPairList[i+1])}"))
                {
                    var rayTracer = (IRayTracer)Activator.CreateInstance(rayTracerPairList[i+1]);
                    EditorCoroutineUtility.StartCoroutine(RunRayTracer(rayTracer), this);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        if (previewTexture != null)
        {
            EditorGUILayout.LabelField(previewDesc);

            var ratio = (float)previewTexture.height / previewTexture.width;
            Vector2 sz = new Vector2(EditorGUIUtility.currentViewWidth, EditorGUIUtility.currentViewWidth * ratio);
            Rect r = EditorGUILayout.GetControlRect(false, GUILayout.Height(sz.y), GUILayout.ExpandHeight(false));
            EditorGUI.DrawPreviewTexture(r, previewTexture);

            if(GUILayout.Button("Save"))
            {
                var path = EditorUtility.SaveFilePanel(
                    "Save texture as PNG",
                    "",
                    "pbr.png",
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
        activeRayTracer = rayTracer;
        previewTexture = null;

        var stopWatch = new Stopwatch();
        stopWatch.Start();

        rayTracer.Run();
        while (!rayTracer.isCompleted)
        {
            yield return waitForOneSecond;
        }

        stopWatch.Stop();
        Debug.Log($"{GetRayTracerTypeName(rayTracer.GetType())} finish running in {stopWatch.Elapsed.TotalSeconds:F2} seconds.");

        previewDesc = $"{GetRayTracerTypeName(activeRayTracer.GetType())}: {rayTracer.desc}";
        previewTexture = rayTracer.texture;
        previewTexture.Apply();
        Repaint();

        rayTracer.Dispose();
        activeRayTracer = null;
    }
} // class MyEditorWindow

} // namespace rtwk
