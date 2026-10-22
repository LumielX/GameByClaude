using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BooTrayal.Editor.BuildPipeline
{
    /// <summary>Windows x64 player build. Run: -executeMethod BooTrayal.Editor.BuildPipeline.WindowsBuild.Build</summary>
    public static class WindowsBuild
    {
        public const string OutputPath = "Builds/Windows/BOO-TRAYAL.exe";

        public static void Build()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };
            var report = UnityEditor.BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[WindowsBuild] {summary.result} in {summary.totalTime}, {summary.totalSize / (1024 * 1024)} MB, errors={summary.totalErrors}");
            if (summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
