﻿﻿﻿// ============================================================================
// 编译期兼容垫片（Scaffold）
// 让 IL2CPP 版反编译出来的源码先能编译，随后逐个替换为 Mono 原生写法。
// 这些类型都是占位，最终需全部消除。
// ============================================================================
using System;
using System.Text;
using BepInEx;
using BepInEx.Logging;

// ---- BepInEx 6 -> BepInEx 5 桥接 ----
namespace BepInEx
{
    public abstract class BasePlugin : BaseUnityPlugin
    {
        public ManualLogSource Log => Logger;
        public abstract void Load();
        private void Awake()
        {
            Load();
        }
    }
}

namespace BepInEx.Core.Logging.Interpolation
{
    public class BepInExLogInterpolatedStringHandler
    {
        protected readonly StringBuilder Sb = new StringBuilder();
        public BepInExLogInterpolatedStringHandler(int literalLength, int formattedCount) { }
        public void AppendLiteral(string s) => Sb.Append(s);
        public void AppendFormatted(object value) => Sb.Append(value);
        public void AppendFormatted(string value) => Sb.Append(value);
        public void AppendFormatted<T>(T value, string format) => Sb.AppendFormat("{0:" + format + "}", value);
        public override string ToString() => Sb.ToString();
    }
    public class BepInExInfoLogInterpolatedStringHandler : BepInExLogInterpolatedStringHandler
    {
        public BepInExInfoLogInterpolatedStringHandler(int l, int f, out bool shouldAppend) : base(l, f) { shouldAppend = true; }
    }
    public class BepInExErrorLogInterpolatedStringHandler : BepInExLogInterpolatedStringHandler
    {
        public BepInExErrorLogInterpolatedStringHandler(int l, int f, out bool shouldAppend) : base(l, f) { shouldAppend = true; }
    }
    public class BepInExWarningLogInterpolatedStringHandler : BepInExLogInterpolatedStringHandler
    {
        public BepInExWarningLogInterpolatedStringHandler(int l, int f, out bool shouldAppend) : base(l, f) { shouldAppend = true; }
    }
    public class BepInExDebugLogInterpolatedStringHandler : BepInExLogInterpolatedStringHandler
    {
        public BepInExDebugLogInterpolatedStringHandler(int l, int f, out bool shouldAppend) : base(l, f) { shouldAppend = true; }
    }
}

// ---- netstandard2.1 没有 DefaultInterpolatedStringHandler，补一个 ----
namespace System.Runtime.CompilerServices
{
    public ref struct DefaultInterpolatedStringHandler
    {
        private readonly StringBuilder _sb;
        public DefaultInterpolatedStringHandler(int literalLength, int formattedCount) { _sb = new StringBuilder(literalLength + formattedCount * 8); }
        public void AppendLiteral(string s) => _sb.Append(s);
        public void AppendFormatted(object v) => _sb.Append(v);
        public void AppendFormatted(string v) => _sb.Append(v);
        public void AppendFormatted(string v, int alignment, string format) => _sb.Append(v);
        public void AppendFormatted<T>(T v) => _sb.Append(v);
        public void AppendFormatted<T>(T v, string format) => _sb.AppendFormat("{0:" + format + "}", v);
        public void AppendFormatted<T>(T v, int alignment) => _sb.Append(v);
        public void AppendFormatted<T>(T v, int alignment, string format) => _sb.AppendFormat("{0:" + format + "}", v);
        public string ToStringAndClear() => _sb.ToString();
        public override string ToString() => _sb.ToString();
    }
}

// ---- TryCast 在 Mono 下退化为类型判定 ----
namespace Haavk
{
    internal static class Il2CppCompat
    {
        internal static T Il2CppAs<T>(this object o) => o is T typed ? typed : default;

        // IL2CPP 下 .Pointer 是原生句柄，用于身份比较；Mono 下用实例身份代理。
        internal static nint Pointer(this UnityEngine.Object o)
            => o == null ? 0 : o.GetInstanceID();

        internal static nint Pointer(this object o)
            => o == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
    }

    // ---- 私有/受保护成员访问（原 IL2CPP 版靠 Il2CppInterop 生成的属性，Mono 无此物） ----
    internal static class PrivAccess
    {
        private static readonly System.Reflection.FieldInfo FIvBg =
            HarmonyLib.AccessTools.Field(typeof(EFT.UI.DragAndDrop.ItemView), "BackgroundColor");
        private static readonly System.Reflection.FieldInfo FIvObg =
            HarmonyLib.AccessTools.Field(typeof(EFT.UI.DragAndDrop.ItemView), "OriginalBackgroundColor");
        private static readonly System.Reflection.FieldInfo FSpecItem =
            HarmonyLib.AccessTools.Field(typeof(EFT.UI.ItemSpecificationPanel), "_item");
        private static readonly System.Reflection.FieldInfo FPanelOwner =
            HarmonyLib.AccessTools.Field(typeof(EFT.UI.ActionPanel), "_owner");
        private static readonly System.Reflection.FieldInfo FUISounds =
            HarmonyLib.AccessTools.Field(typeof(EFT.UI.GUISounds), "_UISounds");
        private static readonly System.Reflection.FieldInfo FAggressor =
            HarmonyLib.AccessTools.Field(typeof(EFT.Player), "LastAggressor");

        internal static UnityEngine.Color ItemViewBg(EFT.UI.DragAndDrop.ItemView v)
            => (UnityEngine.Color)FIvBg.GetValue(v);
        internal static void SetItemViewBg(EFT.UI.DragAndDrop.ItemView v, UnityEngine.Color c)
            => FIvBg.SetValue(v, c);
        internal static UnityEngine.Color ItemViewObg(EFT.UI.DragAndDrop.ItemView v)
            => (UnityEngine.Color)FIvObg.GetValue(v);
        internal static void SetItemViewObg(EFT.UI.DragAndDrop.ItemView v, UnityEngine.Color c)
            => FIvObg.SetValue(v, c);
        internal static EFT.InventoryLogic.Item SpecPanelItem(EFT.UI.ItemSpecificationPanel p)
            => FSpecItem?.GetValue(p) as EFT.InventoryLogic.Item;
        internal static EFT.GamePlayerOwner ActionPanelOwner(EFT.UI.ActionPanel p)
            => FPanelOwner?.GetValue(p) as EFT.GamePlayerOwner;
        internal static UISoundsWrapper GuiSoundsUISounds(EFT.UI.GUISounds g)
            => FUISounds?.GetValue(g) as UISoundsWrapper;
        internal static string PlayerLastAggressorId(EFT.Player p)
            => p == null ? null : (FAggressor?.GetValue(p) as EFT.IPlayer)?.ProfileId;
    }
}

// ---- Il2CppInterop 互操作层占位 ----
namespace Il2CppInterop.Runtime.InteropTypes
{
    public interface IIl2CppObjectBase { IntPtr Pointer { get; } }
    public class Il2CppObjectBase : IIl2CppObjectBase
    {
        public IntPtr Pointer { get; set; }
        public Il2CppObjectBase() { }
        public Il2CppObjectBase(IntPtr p) { Pointer = p; }
        public T TryCast<T>() => default;
        public bool Is<T>() => false;
        public static implicit operator Il2CppObjectBase(IntPtr p) => new Il2CppObjectBase(p);
        public static implicit operator IntPtr(Il2CppObjectBase o) => o.Pointer;
    }
}

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppArrayBase<T> : Il2CppObjectBase
    {
        public Il2CppArrayBase() { }
        public Il2CppArrayBase(IntPtr p) : base(p) { }
        public int Length => 0;
        public T this[int i] { get => default; set { } }
    }
    public class Il2CppReferenceArray<T> : Il2CppArrayBase<T> { public Il2CppReferenceArray() { } public Il2CppReferenceArray(IntPtr p) : base(p) { } }
    public class Il2CppStructArray<T> : Il2CppArrayBase<T> { public Il2CppStructArray() { } public Il2CppStructArray(IntPtr p) : base(p) { } }
}

namespace Il2CppInterop.Runtime
{
    public class Il2CppType : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        public IntPtr Handle => IntPtr.Zero;
    }
    public static class DelegateSupport { }
}

// ---- Il2CppSystem 占位 ----
namespace Il2CppSystem
{
    public class Object : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase { }
    public class Type : Object { }
    public class Delegate : Object { }
    public class Action : Object { }
    public class Action<T> : Object { }
    public class Nullable<T> : Object { }
}

namespace Il2CppSystem.Collections
{
    public interface IEnumerable : Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase { IEnumerator GetEnumerator(); }
    public interface IEnumerator : Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase
    {
        object Current { get; }
        bool MoveNext();
        void Reset();
    }
}

namespace Il2CppSystem.Collections.Generic
{
    public interface IEnumerable<T> : Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase { IEnumerator<T> GetEnumerator(); }
    public interface IEnumerator<T> : Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase
    {
        T Current { get; }
        bool MoveNext();
    }
    public class List<T> : Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase
    {
        public IntPtr Pointer => IntPtr.Zero;
    }
    public class Dictionary<TKey, TValue> : Il2CppInterop.Runtime.InteropTypes.IIl2CppObjectBase
    {
        public IntPtr Pointer => IntPtr.Zero;
    }
}
