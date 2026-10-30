using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace EnemyScaling
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.cojoco.enemyscaling";
        public const string PluginName = "Enemy Scaling";
        public const string PluginVersion = "0.3.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> HpMultiplier;
        internal static ConfigEntry<float> BossHpMultiplier;
        internal static ConfigEntry<float> DamageMultiplier;
        internal static ConfigEntry<float> AoeDamageMultiplier;
        internal static ConfigEntry<bool> ScaleSummons;
        internal static ConfigEntry<float> HpPerExtraHero;
        internal static ConfigEntry<float> BossHpPerExtraHero;
        internal static ConfigEntry<float> DamagePerExtraHero;
        internal static ConfigEntry<float> AoeDamagePerExtraHero;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Turn enemy scaling on or off. In co-op the host's settings are used for everyone.");
            HpMultiplier = Config.Bind("Scaling", "HpMultiplier", 2.0f,
                "Multiplies the max HP of every enemy that is not a boss (applied after the game's own NG+ and difficulty HP changes).");
            BossHpMultiplier = Config.Bind("Scaling", "BossHpMultiplier", 2.0f,
                "Multiplies the max HP of bosses. Set it to the same value as HpMultiplier to treat bosses like everyone else.");
            DamageMultiplier = Config.Bind("Scaling", "DamageMultiplier", 1.5f,
                "Multiplies the damage of enemy cards that hit one target (applied after all of the enemy's own damage bonuses).");
            AoeDamageMultiplier = Config.Bind("Scaling", "AoeDamageMultiplier", 1.0f,
                "Multiplies the damage of enemy cards that hit every hero at once (\"All heroes\" and \"Global\" cards). 1.0 leaves them unchanged.");
            ScaleSummons = Config.Bind("Scaling", "ScaleSummons", true,
                "Also scale the HP and damage of enemies summoned during combat.");

            const string perHero = " The multipliers above are for a team of 4 heroes. With more heroes (e.g. with the More Heroes mod), each hero beyond 4 " +
                                   "adds this fraction of the multiplier: multiplier x (1 + value x (heroes - 4)). 0 = the same for any team size. " +
                                   "No effect on a normal 4-hero team.";
            HpPerExtraHero = Config.Bind("HeroCount", "HpPerExtraHero", 0.25f,
                "Enemy HP (not bosses) for each hero beyond 4. 0.25 doubles HP with 8 heroes." + perHero);
            BossHpPerExtraHero = Config.Bind("HeroCount", "BossHpPerExtraHero", 0.25f,
                "Boss HP for each hero beyond 4. 0.25 doubles boss HP with 8 heroes." + perHero);
            DamagePerExtraHero = Config.Bind("HeroCount", "DamagePerExtraHero", 0.125f,
                "Single-target enemy damage for each hero beyond 4. 0.125 gives x1.5 with 8 heroes." + perHero);
            AoeDamagePerExtraHero = Config.Bind("HeroCount", "AoeDamagePerExtraHero", 0f,
                "\"All heroes\" / \"Global\" enemy damage for each hero beyond 4. 0 by default: those cards already hit every extra hero." + perHero);

            new Harmony(PluginGuid).PatchAll();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Local settings: {ScalingSettings.Local().Describe()}");
        }
    }
}
