namespace Haavk;

// Mono 版：Il2Cpp 的 TryCast 语义在 Mono 下等价于普通类型判定。
internal static class Il2CppCast
{
    internal static T As<T>(object source) => source is T typed ? typed : default;
}
