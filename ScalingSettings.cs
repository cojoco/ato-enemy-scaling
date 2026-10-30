using System;
using System.Globalization;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;

namespace EnemyScaling
{
    // The multipliers in effect right now. Solo: the local config. Co-op: the values the host put in the Photon room properties,
    // because every client simulates combat itself (lockstep with a sync check), so everyone must scale identically.
    internal struct ScalingSettings
    {
        private const string KeyEnabled = "es_on";
        private const string KeyHp = "es_hp";
        private const string KeyBossHp = "es_bosshp";
        private const string KeyDamage = "es_dmg";
        private const string KeyAoeDamage = "es_aoedmg";
        private const string KeySummons = "es_summons";
        private const string KeyHpPerHero = "es_hp_hero";
        private const string KeyBossHpPerHero = "es_bosshp_hero";
        private const string KeyDamagePerHero = "es_dmg_hero";
        private const string KeyAoeDamagePerHero = "es_aoedmg_hero";

        // The base multipliers are for a 4-hero team (the vanilla size); each hero beyond 4 adds the PerExtraHero fraction of them.
        private const int VanillaHeroCount = 4;

        public bool Enabled;
        public float Hp;
        public float BossHp;
        public float Damage;
        public float AoeDamage;
        public bool Summons;
        public float HpPerHero;
        public float BossHpPerHero;
        public float DamagePerHero;
        public float AoeDamagePerHero;
        public string Source;

        private static bool warnedNoHostSettings;
        private static string lastLoggedSource;

        public static ScalingSettings Local()
        {
            return new ScalingSettings
            {
                Enabled = Plugin.Enabled.Value,
                Hp = Clamp(Plugin.HpMultiplier.Value),
                BossHp = Clamp(Plugin.BossHpMultiplier.Value),
                Damage = Clamp(Plugin.DamageMultiplier.Value),
                AoeDamage = Clamp(Plugin.AoeDamageMultiplier.Value),
                Summons = Plugin.ScaleSummons.Value,
                HpPerHero = ClampPerHero(Plugin.HpPerExtraHero.Value),
                BossHpPerHero = ClampPerHero(Plugin.BossHpPerExtraHero.Value),
                DamagePerHero = ClampPerHero(Plugin.DamagePerExtraHero.Value),
                AoeDamagePerHero = ClampPerHero(Plugin.AoeDamagePerExtraHero.Value),
                Source = "local config",
            };
        }

        public static ScalingSettings Current()
        {
            if (!InMultiplayerRoom())
            {
                return Local();
            }
            // Room values win for everyone once they exist, including a player who becomes host after the original host left.
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            if (props == null || !props.ContainsKey(KeyEnabled))
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    // Host before Photon has echoed the published values back (or a room created before the mod loaded).
                    ScalingSettings host = Local();
                    host.Source = "local config (host)";
                    return host;
                }
                if (!warnedNoHostSettings)
                {
                    warnedNoHostSettings = true;
                    Plugin.Log.LogWarning("Co-op game, but the host did not publish Enemy Scaling settings (host has no mod or an old version). Scaling is OFF to stay in sync with the host.");
                }
                return new ScalingSettings { Enabled = false, Hp = 1f, BossHp = 1f, Damage = 1f, AoeDamage = 1f, Summons = false, Source = "host has no settings" };
            }
            float damage = Clamp(ReadFloat(props, KeyDamage, 1f));
            return new ScalingSettings
            {
                Enabled = ReadBool(props, KeyEnabled, false),
                Hp = Clamp(ReadFloat(props, KeyHp, 1f)),
                BossHp = Clamp(ReadFloat(props, KeyBossHp, 1f)),
                Damage = damage,
                // A host on a version without this key scales AoE cards like any other card.
                AoeDamage = Clamp(ReadFloat(props, KeyAoeDamage, damage)),
                Summons = ReadBool(props, KeySummons, true),
                // A host on a version without these keys does not scale with the hero count.
                HpPerHero = ClampPerHero(ReadFloat(props, KeyHpPerHero, 0f)),
                BossHpPerHero = ClampPerHero(ReadFloat(props, KeyBossHpPerHero, 0f)),
                DamagePerHero = ClampPerHero(ReadFloat(props, KeyDamagePerHero, 0f)),
                AoeDamagePerHero = ClampPerHero(ReadFloat(props, KeyAoeDamagePerHero, 0f)),
                Source = "host's room settings",
            };
        }

        // Called on the host when its room is created. Room properties are stored by Photon and handed to every player who joins later.
        public static void PublishToRoom()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }
            ScalingSettings s = Local();
            var table = new Hashtable
            {
                { KeyEnabled, s.Enabled },
                { KeyHp, s.Hp },
                { KeyBossHp, s.BossHp },
                { KeyDamage, s.Damage },
                { KeyAoeDamage, s.AoeDamage },
                { KeySummons, s.Summons },
                { KeyHpPerHero, s.HpPerHero },
                { KeyBossHpPerHero, s.BossHpPerHero },
                { KeyDamagePerHero, s.DamagePerHero },
                { KeyAoeDamagePerHero, s.AoeDamagePerHero },
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(table);
            Plugin.Log.LogInfo($"Published scaling settings to the co-op room: {s.Describe()}");
        }

        // Multipliers for the current team: base x (1 + perHero x (heroes - 4)). With 4 heroes (or fewer) this is the base value.
        public float HpFor(int heroes) => ForHeroes(Hp, HpPerHero, heroes);
        public float BossHpFor(int heroes) => ForHeroes(BossHp, BossHpPerHero, heroes);
        public float DamageFor(int heroes) => ForHeroes(Damage, DamagePerHero, heroes);
        public float AoeDamageFor(int heroes) => ForHeroes(AoeDamage, AoeDamagePerHero, heroes);

        private static float ForHeroes(float baseMultiplier, float perHero, int heroes)
        {
            int extra = Math.Max(0, heroes - VanillaHeroCount);
            return baseMultiplier * (1f + perHero * extra);
        }

        // Heroes in the current run, read from the game itself (AtOManager.team), so this works with or without a mod that adds
        // heroes (More Heroes). In co-op every player has the same team, so everyone gets the same multipliers.
        public static int HeroCount()
        {
            try
            {
                GameTeam team = AtOManager.Instance != null ? AtOManager.Instance.team : null;
                return team != null ? Math.Max(VanillaHeroCount, team.Size) : VanillaHeroCount;
            }
            catch (Exception)
            {
                return VanillaHeroCount;
            }
        }

        // Logs which settings apply once per change of source, so co-op players can check they got the host's values.
        public static void LogIfChanged(ScalingSettings s)
        {
            string line = $"{s.Source}: {s.Describe()}; {s.DescribeFor(HeroCount())}";
            if (line != lastLoggedSource)
            {
                lastLoggedSource = line;
                Plugin.Log.LogInfo($"Scaling settings in use ({line})");
            }
        }

        public string Describe()
        {
            if (!Enabled)
            {
                return "disabled";
            }
            return string.Format(CultureInfo.InvariantCulture, "HP x{0}, boss HP x{1}, damage x{2}, AoE damage x{3}, summons {4}; per hero beyond 4: HP +{5}, boss HP +{6}, damage +{7}, AoE damage +{8}",
                Hp, BossHp, Damage, AoeDamage, Summons ? "scaled" : "not scaled", HpPerHero, BossHpPerHero, DamagePerHero, AoeDamagePerHero);
        }

        public string DescribeFor(int heroes)
        {
            if (!Enabled)
            {
                return $"{heroes} heroes";
            }
            return string.Format(CultureInfo.InvariantCulture, "with {0} heroes: HP x{1:0.###}, boss HP x{2:0.###}, damage x{3:0.###}, AoE damage x{4:0.###}",
                heroes, HpFor(heroes), BossHpFor(heroes), DamageFor(heroes), AoeDamageFor(heroes));
        }

        private static bool InMultiplayerRoom()
        {
            return GameManager.Instance != null && GameManager.Instance.IsMultiplayer() && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
        }

        private static float Clamp(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 1f;
            }
            return value;
        }

        private static float ClampPerHero(float value)
        {
            if (float.IsNaN(value) || value < 0f)
            {
                return 0f;
            }
            return value;
        }

        private static float ReadFloat(Hashtable props, string key, float fallback)
        {
            try
            {
                return props.TryGetValue(key, out object v) && v != null ? Convert.ToSingle(v, CultureInfo.InvariantCulture) : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static bool ReadBool(Hashtable props, string key, bool fallback)
        {
            return props.TryGetValue(key, out object v) && v is bool b ? b : fallback;
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnCreatedRoom))]
    internal static class OnCreatedRoomPatch
    {
        private static void Postfix()
        {
            try
            {
                ScalingSettings.PublishToRoom();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not publish scaling settings to the room: {e}");
            }
        }
    }
}
