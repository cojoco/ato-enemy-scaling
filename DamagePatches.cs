using System;
using HarmonyLib;

namespace EnemyScaling
{
    // Damage scaling. Character.DamageWithCharacterBonus is called on the attacker for every direct card hit
    // (Damage/DamageMechanics.DealDamageToTarget) and also for the numbers shown on enemy cards and target previews
    // (CardRealtimeData precalculated damage, MatchManager previews), so one Postfix covers both the hit and what is displayed.
    // The game applies its own NG+ and sandbox monster damage changes inside this method for non-heroes; ours goes on top.
    [HarmonyPatch(typeof(Character), nameof(Character.DamageWithCharacterBonus))]
    internal static class DamageWithCharacterBonusPatch
    {
        private static void Postfix(Character __instance, ref int __result)
        {
            if (__result <= 0 || __instance.IsHero || !(__instance is NPC))
            {
                return;
            }
            try
            {
                ScalingSettings s = ScalingSettings.Current();
                if (!s.Enabled || Math.Abs(s.Damage - 1f) < 0.0001f)
                {
                    return;
                }
                if (__instance.IsSummon && !s.Summons)
                {
                    return;
                }
                __result = Math.Max(1, Functions.FuncRoundToInt(__result * s.Damage));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Damage scaling failed: {e}");
            }
        }
    }
}
