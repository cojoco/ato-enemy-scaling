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
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> HpMultiplier;
        internal static ConfigEntry<float> BossHpMultiplier;
        internal static ConfigEntry<float> DamageMultiplier;
        internal static ConfigEntry<float> AoeDamageMultiplier;
        internal static ConfigEntry<bool> ScaleSummons;

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

            new Harmony(PluginGuid).PatchAll();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Local settings: {ScalingSettings.Local().Describe()}");
        }
    }
}
