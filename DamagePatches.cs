using System;
using BattleMatch;
using Cards;
using Damage;
using HarmonyLib;

namespace EnemyScaling
{
    // Damage scaling. Character.DamageWithCharacterBonus is called on the attacker for every direct card hit
    // (Damage/DamageMechanics.DealDamageToTarget) and also for the numbers shown on enemy cards
    // (CardRealtimeData.UpdatePrecalculatedDamages), so one Postfix covers both the hit and what is displayed.
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
                if (!s.Enabled)
                {
                    return;
                }
                float multiplier = CardContext.IsAoe() ? s.AoeDamage : s.Damage;
                if (Math.Abs(multiplier - 1f) < 0.0001f)
                {
                    return;
                }
                if (__instance.IsSummon && !s.Summons)
                {
                    return;
                }
                __result = Math.Max(1, Functions.FuncRoundToInt(__result * multiplier));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Damage scaling failed: {e}");
            }
        }
    }

    // DamageWithCharacterBonus does not get the card, so the two card-damage callers record it here for the duration of the call.
    // Damage from items, traits and pets has no card and uses the normal damage multiplier.
    internal static class CardContext
    {
        internal static CardRealtimeData Current;

        // Global target type is "every target on a side": with the Enemy side an enemy card reads "All heroes", with Anyone "Global".
        internal static bool IsAoe()
        {
            return Current != null && Current.TargetType == Enums.CardTargetType.Global;
        }
    }

    // async method: the patch wraps only the synchronous start, which is where DamageWithCharacterBonus is called (before the
    // first await), so the context is set for that call and restored right after.
    [HarmonyPatch(typeof(DamageMechanics), nameof(DamageMechanics.DealDamageToTarget))]
    internal static class DealDamageToTargetPatch
    {
        private static void Prefix(CastCardActionData data, out CardRealtimeData __state)
        {
            __state = CardContext.Current;
            CardContext.Current = data.CardActive;
        }

        private static void Postfix(CardRealtimeData __state)
        {
            CardContext.Current = __state;
        }
    }

    [HarmonyPatch(typeof(CardRealtimeData), nameof(CardRealtimeData.UpdatePrecalculatedDamages))]
    internal static class UpdatePrecalculatedDamagesPatch
    {
        private static void Prefix(CardRealtimeData __instance, out CardRealtimeData __state)
        {
            __state = CardContext.Current;
            CardContext.Current = __instance;
        }

        private static void Postfix(CardRealtimeData __state)
        {
            CardContext.Current = __state;
        }
    }
}
