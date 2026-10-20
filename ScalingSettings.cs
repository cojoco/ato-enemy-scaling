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
        private const string KeySummons = "es_summons";

        public bool Enabled;
        public float Hp;
        public float BossHp;
        public float Damage;
        public bool Summons;
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
                Summons = Plugin.ScaleSummons.Value,
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
                return new ScalingSettings { Enabled = false, Hp = 1f, BossHp = 1f, Damage = 1f, Summons = false, Source = "host has no settings" };
            }
            return new ScalingSettings
            {
                Enabled = ReadBool(props, KeyEnabled, false),
                Hp = Clamp(ReadFloat(props, KeyHp, 1f)),
                BossHp = Clamp(ReadFloat(props, KeyBossHp, 1f)),
                Damage = Clamp(ReadFloat(props, KeyDamage, 1f)),
                Summons = ReadBool(props, KeySummons, true),
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
                { KeySummons, s.Summons },
            };
            PhotonNetwork.CurrentRoom.SetCustomProperties(table);
            Plugin.Log.LogInfo($"Published scaling settings to the co-op room: {s.Describe()}");
        }

        // Logs which settings apply once per change of source, so co-op players can check they got the host's values.
        public static void LogIfChanged(ScalingSettings s)
        {
            string line = $"{s.Source}: {s.Describe()}";
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
            return string.Format(CultureInfo.InvariantCulture, "HP x{0}, boss HP x{1}, damage x{2}, summons {3}", Hp, BossHp, Damage, Summons ? "scaled" : "not scaled");
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
