using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using Newtonsoft.Json;

namespace SptJsonConverterFix
{
    /// <summary>
    /// Guarantees that JsonConvert.DefaultSettings carries EFT's own JsonConverter set.
    ///
    /// Why this is needed on this install:
    ///   EFT's ItemDeserializer.CreateItem does
    ///       properties.JToken.ToObject&lt;Dictionary&lt;string, UnparsedData&gt;&gt;()
    ///   JToken.ToObject(T) goes through JsonSerializer.CreateDefault(), which builds the
    ///   serializer from JsonConvert.DefaultSettings. If UnparsedDataConverter is not in
    ///   there, every object-shaped item `upd` member silently becomes an UnparsedData with
    ///   JToken == null (later -> JsonExtensions.ParseJsonTo -> PopulateObject("null") ->
    ///   "Unexpected initial token 'Null'"), and every scalar-shaped member throws
    ///   "Could not cast or convert from System.Int64 to UnparsedData". Both floods item
    ///   deserialization, fails FlatItemsToTree, and freezes the raid.
    ///
    ///   The only code that ever assigns JsonConvert.DefaultSettings is
    ///   Newtonsoft.Json.UnityConverters.UnityConverterInitializer, which builds its
    ///   converter list at static-field-init time via reflection over every loaded
    ///   assembly. Its helper GetLoadableTypes() only catches ReflectionTypeLoadException,
    ///   so a single mod assembly whose GetTypes() throws anything else (a TypeLoadException
    ///   from a mod built for an older SPT, for example) takes the whole type initializer
    ///   down and DefaultSettings is left null.
    ///
    ///   This plugin does not try to find the culprit. It simply re-establishes the
    ///   invariant: whatever DefaultSettings is (or is not), make sure it contains the
    ///   converters from EFT.EftJsonConverters.Converters, which is the authoritative list
    ///   the game itself uses everywhere it passes converters explicitly.
    ///
    /// Safe by construction: converters already present (matched by type) are not added
    /// twice, the previous settings object is reused rather than replaced, and every step
    /// is exception-guarded so a failure here can never make things worse than the state
    /// it found.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class JsonConverterFixPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.haavk.jsonconvfix";
        public const string PluginName = "SPT Json Converter Fix";
        public const string PluginVersion = "1.0.0";

        private void Awake()
        {
            try
            {
                Install();
            }
            catch (Exception e)
            {
                Logger.LogError(PluginName + ": install failed -> " + e);
            }
        }

        private void Install()
        {
            // ---- 1. Report the state we inherited. This is the decisive diagnostic. ----
            Func<JsonSerializerSettings> inherited = JsonConvert.DefaultSettings;
            JsonSerializerSettings inheritedSettings = null;
            string inheritedError = "-";

            if (inherited != null)
            {
                try
                {
                    inheritedSettings = inherited();
                }
                catch (Exception e)
                {
                    inheritedError = e.GetType().Name + ": " + e.Message;
                }
            }

            Logger.LogInfo(string.Format(
                "[diag] inherited JsonConvert.DefaultSettings = {0}; settings = {1}; converters = {2}; invokeError = {3}",
                inherited == null ? "NULL" : "set",
                inheritedSettings == null ? "null" : inheritedSettings.GetType().Name,
                inheritedSettings == null ? "n/a" : inheritedSettings.Converters.Count.ToString(),
                inheritedError));

            if (inheritedSettings != null)
            {
                Logger.LogInfo(string.Format(
                    "[diag] inherited has UnparsedDataConverter={0} has StringEnumConverter={1}",
                    HasConverter(inheritedSettings, "UnparsedDataConverter"),
                    HasConverter(inheritedSettings, "StringEnumConverter")));
            }

            // ---- 2. Read EFT's authoritative converter array. ----
            List<JsonConverter> eftConverters = GetEftConverters(out string source);
            Logger.LogInfo(string.Format(
                "[diag] EFT converters: source={0}; count={1}", source, eftConverters == null ? -1 : eftConverters.Count));

            // ---- 3. Pick the object to extend: keep whatever exists, never discard it. ----
            JsonSerializerSettings settings = inheritedSettings;
            if (settings == null)
            {
                settings = TryGetUnityDefaultSettings();
                Logger.LogInfo("[diag] base settings = " + (settings != null ? "UnityConverterInitializer.DefaultUnityConvertersSettings" : "new JsonSerializerSettings()"));
                if (settings == null)
                {
                    settings = new JsonSerializerSettings();
                }
            }

            // ---- 4. Union by runtime type. ----
            int added = 0;
            if (eftConverters != null)
            {
                foreach (JsonConverter converter in eftConverters)
                {
                    if (converter == null)
                    {
                        continue;
                    }

                    Type type = converter.GetType();
                    if (settings.Converters.Any(existing => existing != null && existing.GetType() == type))
                    {
                        continue;
                    }

                    try
                    {
                        settings.Converters.Add(converter);
                        added++;
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning("[diag] could not add " + type.FullName + " -> " + e.Message);
                    }
                }
            }

            // ---- 5. Freeze and publish. ----
            JsonSerializerSettings frozen = settings;
            JsonConvert.DefaultSettings = () => frozen;

            Logger.LogInfo(string.Format(
                "[diag] applied: added={0}; UnparsedDataConverter present now={1}; total converters={2}",
                added, HasConverter(frozen, "UnparsedDataConverter"), frozen.Converters.Count));
            Logger.LogInfo(PluginName + ": JsonConvert.DefaultSettings now carries EFT's converters (UnparsedDataConverter included).");
        }

        private static bool HasConverter(JsonSerializerSettings settings, string typeName)
        {
            foreach (JsonConverter converter in settings.Converters)
            {
                if (converter != null && converter.GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<JsonConverter> GetEftConverters(out string source)
        {
            // Primary: EFT.EftJsonConverters.Converters -- the exact array the game uses.
            Type eftJsonConverters = FindType("EFT.EftJsonConverters");
            if (eftJsonConverters != null)
            {
                object raw = ReadStaticMember(eftJsonConverters, "Converters");
                if (raw is JsonConverter[] array)
                {
                    List<JsonConverter> list = array.Where(c => c != null).ToList();

                    // The converter this whole fix exists for must be in there. If the array
                    // somehow does not carry it, append a dedicated instance below.
                    if (!list.Any(c => c.GetType().Name == "UnparsedDataConverter"))
                    {
                        JsonConverter extra = TryCreateConverter("EFT.UnparsedDataConverter") ?? TryCreateConverter("UnparsedDataConverter");
                        if (extra != null)
                        {
                            list.Add(extra);
                        }
                    }

                    source = "EFT.EftJsonConverters.Converters";
                    return list;
                }
            }

            // Fallback: build the one converter that matters directly.
            JsonConverter single = TryCreateConverter("EFT.UnparsedDataConverter") ?? TryCreateConverter("UnparsedDataConverter");
            if (single != null)
            {
                source = "fallback:UnparsedDataConverter only";
                return new List<JsonConverter> { single };
            }

            source = "not found";
            return null;
        }

        private static JsonSerializerSettings TryGetUnityDefaultSettings()
        {
            try
            {
                Type initializer = FindType("Newtonsoft.Json.UnityConverters.UnityConverterInitializer");
                if (initializer == null)
                {
                    return null;
                }

                PropertyInfo property = initializer.GetProperty(
                    "DefaultUnityConvertersSettings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return property?.GetValue(null, null) as JsonSerializerSettings;
            }
            catch (Exception e)
            {
                // Expected on a broken install: the type initializer itself is what failed.
                BepInEx.Logging.Logger.CreateLogSource("SptJsonConverterFix")
                    .LogWarning("[diag] UnityConverterInitializer.DefaultUnityConvertersSettings unavailable -> " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // A single unloadable assembly must not stop the search.
                }
            }

            return null;
        }

        private static object ReadStaticMember(Type type, string name)
        {
            try
            {
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                return property?.GetValue(null, null);
            }
            catch
            {
                return null;
            }
        }

        private static JsonConverter TryCreateConverter(string fullName)
        {
            Type type = FindType(fullName);
            if (type == null)
            {
                return null;
            }

            try
            {
                return Activator.CreateInstance(type, true) as JsonConverter;
            }
            catch
            {
                return null;
            }
        }
    }
}
