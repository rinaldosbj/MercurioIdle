using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class DysonReleaseBuild
{
    public static void Build()
    {
        DysonSwarmValidation.ValidateMirrorMapping();
        DysonSwarmValidation.ValidateGrowthContinuity();
        DysonSwarmValidation.Validate();
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/SunShaderTest.unity" },
            locationPathName = "Builds/Sun/MercurioIdle.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        Directory.CreateDirectory("Library/DysonSwarm");
        File.WriteAllText("Library/DysonSwarm/ReleaseBuild.txt",
            $"{result.summary.result} errors={result.summary.totalErrors} time={result.summary.totalTime} UTC={DateTime.UtcNow:o}");
        if (result.summary.result != BuildResult.Succeeded)
            throw new Exception("Sun release build failed.");
    }
}
