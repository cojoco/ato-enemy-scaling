using System;
using System.Runtime.CompilerServices;
using BattleMatch;
using Cards;
using HarmonyLib;

namespace EnemyScaling
{
    // HP scaling. NPC.InitData always starts again from NpcData.Hp, so scaling in its Postfix is idempotent: the game calls InitData
    // two or three times for the same enemy (BattleTeamManager.InitNPCTeam, the spawn loop, twice inside CreateNPC) and each call
    // gives the same result. Resumed combats restore the saved (already scaled) HP afterwards in MatchManager.FixTOTALCo.

    internal class ScaleInfo
    {
        public int BaseHp;
        public int ScaledHp;
        public float Multiplier;
        public string Skipped;
    }

    internal static class SpawnContext
    {
        // Set while BattleTeamManager.CreateNPC runs, so InitData/NPCItem.Init know why the enemy is being created.
        public static bool CreatingSummon;
        public static bool CreatingFromReload;

        public static readonly ConditionalWeakTable<NPC, ScaleInfo> Info = new ConditionalWeakTable<NPC, ScaleInfo>();
    }

    [HarmonyPatch(typeof(NPC), nameof(NPC.InitData))]
    internal static class NpcInitDataPatch
    {
        private static void Postfix(NPC __instance)
        {
            try
            {
                if (__instance.NpcData == null)
                {
                    return;
                }
                ScalingSettings s = ScalingSettings.Current();
                var info = new ScaleInfo { BaseHp = __instance.Hp, ScaledHp = __instance.Hp, Multiplier = 1f };
                SpawnContext.Info.Remove(__instance);
                SpawnContext.Info.Add(__instance, info);

                if (!s.Enabled)
                {
                    info.Skipped = "scaling disabled";
                    return;
                }
                if (SpawnContext.CreatingSummon && !s.Summons)
                {
                    info.Skipped = "summon, ScaleSummons off";
                    return;
                }
                float mult = __instance.NpcData.IsBoss ? s.BossHp : s.Hp;
                info.Multiplier = mult;
                if (Math.Abs(mult - 1f) < 0.0001f)
                {
                    return;
                }
                int scaled = Math.Max(1, Functions.FuncRoundToInt(__instance.Hp * mult));
                __instance.Hp = scaled;
                __instance.HpCurrent = scaled;
                info.ScaledHp = scaled;
                if (__instance.NPCItem != null)
                {
                    __instance.SetHP();
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"HP scaling failed: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(BattleTeamManager), nameof(BattleTeamManager.CreateNPC))]
    internal static class CreateNpcPatch
    {
        private static void Prefix(bool generateFromReload, CardRealtimeData _cardActive, bool isSummon, out bool[] __state)
        {
            __state = new[] { SpawnContext.CreatingSummon, SpawnContext.CreatingFromReload };
            bool replaces = false;
            if (!generateFromReload && _cardActive != null)
            {
                try
                {
                    // Metamorph/evolve turn an existing enemy into another one; treat that as the same enemy, not a summon.
                    replaces = _cardActive.Metamorph || _cardActive.Evolve;
                }
                catch (Exception)
                {
                }
            }
            SpawnContext.CreatingSummon = isSummon && !replaces;
            SpawnContext.CreatingFromReload = generateFromReload;
        }

        private static void Finalizer(bool[] __state)
        {
            if (__state != null)
            {
                SpawnContext.CreatingSummon = __state[0];
                SpawnContext.CreatingFromReload = __state[1];
            }
        }
    }

    // NPCItem.Init runs once per enemy that actually appears on screen (combat start, summons, resume), after InitData, so log here.
    [HarmonyPatch(typeof(NPCItem), nameof(NPCItem.Init))]
    internal static class NpcItemInitPatch
    {
        private static void Postfix(NPC _npc)
        {
            try
            {
                if (_npc?.NpcData == null || !SpawnContext.Info.TryGetValue(_npc, out ScaleInfo info))
                {
                    return;
                }
                ScalingSettings.LogIfChanged(ScalingSettings.Current());
                string kind = _npc.NpcData.IsBoss ? " [boss]" : "";
                if (SpawnContext.CreatingSummon)
                {
                    kind += " [summon]";
                }
                string line = $"Enemy {_npc.SourceName} ({_npc.GameName}){kind}: HP {info.BaseHp} -> {info.ScaledHp}";
                if (info.Skipped != null)
                {
                    line += $" (not scaled: {info.Skipped})";
                }
                else if (info.ScaledHp != info.BaseHp)
                {
                    line += $" (x{info.Multiplier})";
                }
                if (SpawnContext.CreatingFromReload)
                {
                    line += " (resumed combat: saved HP is restored next)";
                }
                Plugin.Log.LogInfo(line);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Logging enemy failed: {e}");
            }
        }
    }
}
