using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SptRaidTimeFix
{
    /// <summary>
    /// Normalizes the outbound body of /singleplayer/settings/getRaidTime.
    ///
    /// SPT 4.1.5 client sends {"Side":0,...} (ESideType serialized as an integer by
    /// SPT.Common.Utils.Json, which uses bare JsonConvert.SerializeObject with no global
    /// DefaultSettings). The server model GetRaidTimeRequest.Side is a string and compares
    /// it against "pmc" (OrdinalIgnoreCase); System.Text.Json therefore throws
    /// "The JSON value could not be converted to System.String. Path: $.Side", the route
    /// returns an empty body, and the client then NREs on the null RaidTimeResponse.
    ///
    /// This plugin only rewrites that single field for that single route, and only when it
    /// is a JSON integer. Everything else passes through untouched. Idempotent: if Side is
    /// already a string it is left alone.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RaidTimeSideFixPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.haavk.raidtimefix";
        public const string PluginName = "SPT RaidTime Side Fix";
        public const string PluginVersion = "1.0.0";

        private const string Route = "/singleplayer/settings/getRaidTime";
        private Harmony _harmony;

        private void Awake()
        {
            try
            {
                MethodInfo target = AccessTools.Method(
                    "SPT.Common.Http.RequestHandler:PostJson",
                    new[] { typeof(string), typeof(string) });

                if (target == null)
                {
                    Logger.LogWarning(PluginName + ": SPT.Common.Http.RequestHandler.PostJson(string,string) not found; fix not applied.");
                    return;
                }

                MethodInfo prefix = typeof(RaidTimeSideFixPlugin).GetMethod(
                    nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic);

                _harmony = new Harmony(PluginGuid);
                _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                Logger.LogInfo(PluginName + ": installed on " + target.FullDescription());
            }
            catch (Exception e)
            {
                Logger.LogError(PluginName + ": install failed -> " + e);
            }
        }

        // Harmony prefix: rewrite the request body in place before it is sent.
        private static void Prefix(string path, ref string json)
        {
            try
            {
                if (!string.Equals(path, Route, StringComparison.Ordinal) || string.IsNullOrEmpty(json))
                {
                    return;
                }

                JObject obj = JObject.Parse(json);
                JToken side = obj["Side"];
                if (side == null || side.Type != JTokenType.Integer)
                {
                    return;
                }

                int value = side.Value<int>();
                string name;
                switch (value)
                {
                    case 0: name = "Pmc"; break;
                    case 1: name = "Savage"; break;
                    case 2: name = "Random"; break;
                    default: name = value.ToString(); break;
                }

                obj["Side"] = name;
                json = obj.ToString(Formatting.None);
            }
            catch
            {
                // Never break a request: on any failure fall through with the original json.
            }
        }
    }
}
