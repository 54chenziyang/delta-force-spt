using System;

namespace Haavk.Diagnostics;

// Mono（EFT 0.16.9）下没有 EFT.UI.CharacterSelectionScreen / CharacterSelectionScreenController，
// 该功能（自动选中 PvE 档位）无法移植，这里降级为空实现，保持插件调用点不变。
internal static class AutoSelectPve
{
    internal static bool Enabled => false;

    internal static bool TriggerPresent() => false;

    internal static void Enable() { }
}
