using System.Linq;
using EFT.Hideout;
using HarmonyLib;

namespace DeltaInventory
{
    /// <summary>
    /// Hideout auto-pay, client half.
    ///
    /// Vanilla locks the "UPGRADE" button as soon as one requirement of the next stage is
    /// missing (<see cref="AreaData.DecideStatus"/> -> <see cref="AreaData.CanBuild(Stage)"/>).
    /// We let the button through when the only unmet requirements are consumable items, so the
    /// server can charge their handbook value in roubles and finish the upgrade.
    ///
    /// Non-item requirements (area level, skill, trader loyalty, tools, ...) still have to be
    /// fulfilled - auto-pay must not skip progression gates.
    /// </summary>
    [HarmonyPatch]
    internal static class HideoutAutoPayClient
    {
        private static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AreaData), nameof(AreaData.CanBuild), new[] { typeof(Stage) });
        }

        private static void Postfix(Stage stage, ref bool __result)
        {
            if (__result) return;

            try
            {
                RelatedRequirements requirements = stage != null ? stage.Requirements : null;
                if (requirements == null || !requirements.Any()) return;

                bool hasPayableItem = false;
                foreach (Requirement requirement in requirements)
                {
                    if (requirement == null) return;

                    if (requirement.Type == ERequirementType.Item)
                    {
                        hasPayableItem = true;
                        continue;
                    }

                    if (!requirement.Fulfilled) return;
                }

                if (!hasPayableItem) return;

                __result = true;
                DeltaInventoryPlugin.Verbose("hideout auto-pay: item requirements bypassed, button unlocked");
            }
            catch
            {
                // Never break the vanilla result.
            }
        }
    }
}
