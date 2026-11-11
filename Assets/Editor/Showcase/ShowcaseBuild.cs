// 시연용 Boss Room 을 명령줄에서 Android APK · Linux 전용 서버 · 맥 시험 빌드로 굽는 진입점
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class ShowcaseBuild
{
    const string AppId = "com.maxihan.bossroom";

    public static void Android()
    {
        PlayerSettings.productName = "Boss Room";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Build(BuildTarget.Android, "Builds/android/bossroom.apk");
    }

    // control_cli instrument 를 돌린 뒤 부른다 — CONTROL_CLI 가 있어야 SDK 코드가 빌드에 들어간다(게임 케이스가 이 APK 로 돈다)
    public static void AndroidQa()
    {
        PlayerSettings.productName = "Boss Room";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Build(BuildTarget.Android, "Builds/android/bossroom-qa.apk", defines: new[] { "CONTROL_CLI" });
    }

    public static void LinuxServer()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        Build(BuildTarget.StandaloneLinux64, "Builds/linux-server/BossRoomServer.x86_64", (int)StandaloneBuildSubtarget.Server);
    }

    // 맥에서 서버 하나 · 화면 없는 접속기 하나를 띄워 접속을 확인하는 데만 쓴다
    public static void MacSmoke()
    {
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX);
        Build(BuildTarget.StandaloneOSX, "Builds/mac/BossRoom.app");
    }

    static void Build(BuildTarget target, string path, int subtarget = 0, string[] defines = null)
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            target = target,
            subtarget = subtarget,
            locationPathName = path,
            extraScriptingDefines = defines,
        }).summary;
        // 배치 모드에서 예외를 던지면 Unity 가 종료 코드 1 로 끝나 스크립트가 실패를 알아챈다
        if (summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"빌드 실패({target}): {summary.result}, 오류 {summary.totalErrors}건");
    }
}
