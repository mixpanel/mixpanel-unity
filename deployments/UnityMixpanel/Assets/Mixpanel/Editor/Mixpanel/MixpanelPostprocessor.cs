#if UNITY_IPHONE
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.Diagnostics;

public class MixpanelPostprocessScript : MonoBehaviour
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuildProject)
    {
        UnityEngine.Debug.Log("******** START Mixpanel iOS Postprocess Script ********");

        var scriptSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS);

        Process process = new Process();
        process.StartInfo.FileName = "python";
        process.StartInfo.Arguments = string.Format("Assets/Mixpanel/Editor/Mixpanel/post_process.py \"{0}\" \"{1}\"", pathToBuildProject, scriptSymbols);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        while (!process.StandardOutput.EndOfStream) {
            UnityEngine.Debug.Log(process.StandardOutput.ReadLine());
        }

        UnityEngine.Debug.Log("******** END Mixpanel iOS Postprocess Script ********");
    }
}
#endif
