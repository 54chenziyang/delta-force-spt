using System;
using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.SessionEnd;
using HarmonyLib;
using UnityEngine;

namespace DeltaAutoHeal
{
    /// <summary>
    /// After each raid, automatically selects all treatments, pays whatever money is
    /// available and applies them, then advances past the treatment screen.
    /// If there is not enough money the remaining cost is waived (money is drained to 0).
    /// </summary>
    [BepInPlugin("com.haavk.deltaautoheal", "Delta Auto Heal", "1.0.0")]
    public class DeltaAutoHealPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> AutoHeal;

        // HealthTreatmentServiceView._moneyInStash (private int)
        private static readonly FieldInfo MoneyInStashField =
            AccessTools.Field(typeof(HealthTreatmentServiceView), "_moneyInStash");

        private void Awake()
        {
            Log = Logger;
            AutoHeal = Config.Bind(
                "General",
                "Auto Heal After Raid",
                true,
                "Automatically pay for and apply every treatment after a raid and skip the treatment screen. If money is short the cost is waived (money drops to 0).");

            new Harmony("com.haavk.deltaautoheal").PatchAll(typeof(DeltaAutoHealPlugin).Assembly);
            Log.LogInfo("Delta Auto Heal v1.0.0 loaded");
        }

        [HarmonyPatch(typeof(HealthTreatmentScreen), "Show",
            new Type[]
            {
                typeof(Profile),
                typeof(InventoryController),
                typeof(OfflineHealthController),
                typeof(ItemUiContext),
                typeof(IEftSession)
            })]
        private static class PatchTreatmentScreenShow
        {
            private static void Postfix(HealthTreatmentScreen __instance)
            {
                if (!AutoHeal.Value)
                {
                    return;
                }

                try
                {
                    __instance.StartCoroutine(Run(__instance));
                }
                catch (Exception e)
                {
                    Log.LogError("Delta Auto Heal: could not start routine -> " + e);
                }
            }
        }

        private static IEnumerator Run(HealthTreatmentScreen screen)
        {
            // Wait one frame so the screen and its service view are fully initialised.
            yield return null;

            HealthTreatmentServiceView view = null;
            try
            {
                view = screen._healthTreatmentView;
            }
            catch (Exception e)
            {
                Log.LogError("Delta Auto Heal: view access failed -> " + e);
            }

            if (view == null)
            {
                yield break;
            }

            try
            {
                // Bypass the "not enough money" short-circuit in ApplyTreatments().
                if (MoneyInStashField != null)
                {
                    MoneyInStashField.SetValue(view, int.MaxValue);
                }

                // Make sure everything that can be treated is selected.
                view.OnSelectAll(true);

                // Pay with whatever is available (drains money to 0 if short) and heal.
                view.ApplyTreatments();

                // Hide the treatment UI so it never sits in front of the player.
                view.gameObject.SetActive(false);

                // Same as clicking the "Next" button -> back to the main menu.
                screen._nextButton.OnClick.Invoke();

                Log.LogInfo("Delta Auto Heal: treatments applied, advancing to main menu.");
            }
            catch (Exception e)
            {
                Log.LogError("Delta Auto Heal: failed -> " + e);

                // Fall back to the normal flow so the player is never stuck.
                try { screen._nextButton.OnClick.Invoke(); }
                catch { }
            }
        }
    }
}
