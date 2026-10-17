using System;
using HarmonyLib;

namespace Haavk.Diagnostics;

// SPT 4.1.5 (Mono) 的 ConsoleScreen.Processor 只有 RegisterCommand(string, Action, string)，
// 没有 IL2CPP 版的 RegisterAnonymousCommand(Type[], ...)。验收命令依赖 IL2CPP 特有的
// 委托/类型数组适配，本移植版不启用（功能默认关闭，仅影响手动验收）。
internal static class RaidDebugCommands
{
	internal static void Enable(Harmony harmony)
	{
		HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk] 验收控制台命令（haavk_hop / haavk_cfg）在 SPT 4.1.5 上未适配，已跳过注册");
	}
}
