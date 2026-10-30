# Enemy Scaling

A BepInEx 5 mod for **Across the Obelisk** that makes enemies tougher: it multiplies enemy HP and enemy card damage by configurable factors. It works in the normal 4-hero game, solo or co-op. (It is also the first step of a larger "8 heroes" project, where enemies need to scale up for a bigger party.)

**Status:** used in single-player runs since 2026-10-19 (together with More Heroes); enemy HP scaling is confirmed in game (the log shows HP x2). Co-op has not been tested yet. Back up your save before using it on a run you care about.

## Related mods

- [More Heroes](https://github.com/cojoco/ato-more-heroes): up to 8 heroes in a single-player run. Needs Enemy Scaling.
- [More Players](https://github.com/cojoco/ato-more-players): co-op with more than 4 heroes and rooms for up to 8 players. Needs More Heroes.

## What it does

- **Enemy HP** is multiplied when each enemy is created, after the game's own NG+, challenge-trait and sandbox HP changes. The HP bar shows the new max HP. Bosses (`NpcData.IsBoss`) use their own multiplier.
- **Enemy damage** is multiplied after all of the enemy's own damage bonuses (NG+, traits, items, sandbox). The numbers shown on enemy cards and in attack previews include the multiplier, because the game computes them with the same method.
- **AoE damage** (enemy cards that hit every hero at once: "All heroes" and "Global" cards) has its own multiplier, `AoeDamageMultiplier`, which is `1.0` (unchanged) by default.
- **Bigger teams** (with [More Heroes](https://github.com/cojoco/ato-more-heroes) or any mod that adds heroes): the multipliers above are for a 4-hero team, and each hero beyond 4 adds a configurable fraction of them: `multiplier x (1 + PerExtraHero x (heroes - 4))`. The defaults double enemy HP and give x1.5 single-target damage with 8 heroes (on top of the base multipliers). The hero count is read from the game's own team, so Enemy Scaling does not need More Heroes; with a normal 4-hero team these settings do nothing.
- **Summoned enemies** (minions created during combat) are scaled too, unless `ScaleSummons` is off. An enemy that transforms (metamorph/evolve) is treated as the same enemy and always scaled.
- Results are rounded to whole numbers and never drop below 1.
- **No Steam leaderboards.** Scores from runs played with this mod installed are not posted to the Steam leaderboards (normal, Obelisk, Singularity and weekly), so modded runs do not mix with vanilla ones. The log says "Steam leaderboard score not posted" when a run ends.

## Settings (`BepInEx/config/com.cojoco.enemyscaling.cfg`)

| Section / key | Default | Meaning |
|---|---|---|
| `[General] Enabled` | `true` | Turn scaling on or off |
| `[Scaling] HpMultiplier` | `2.0` | Max HP multiplier for enemies that are not bosses |
| `[Scaling] BossHpMultiplier` | `2.0` | Max HP multiplier for bosses (set it equal to `HpMultiplier` to treat bosses the same) |
| `[Scaling] DamageMultiplier` | `1.5` | Damage multiplier for enemy cards that hit one target |
| `[Scaling] AoeDamageMultiplier` | `1.0` | Damage multiplier for enemy cards that hit every hero (`1.0` = unchanged) |
| `[Scaling] ScaleSummons` | `true` | Also scale HP and damage of enemies summoned mid-combat |
| `[HeroCount] HpPerExtraHero` | `0.25` | Each hero beyond 4 adds this fraction of `HpMultiplier` (8 heroes: x2) |
| `[HeroCount] BossHpPerExtraHero` | `0.25` | The same for `BossHpMultiplier` |
| `[HeroCount] DamagePerExtraHero` | `0.125` | The same for `DamageMultiplier` (8 heroes: x1.5) |
| `[HeroCount] AoeDamagePerExtraHero` | `0` | The same for `AoeDamageMultiplier`; 0 because those cards already hit every extra hero |

With 5 heroes and the defaults (base `1.0`): HP x1.25, damage x1.125; 6 heroes: x1.5 / x1.25; 7: x1.75 / x1.375; 8: x2 / x1.5.

**Upgrading from 0.2.x with More Heroes:** 0.2.x had no hero-count settings, so the base multipliers (`2.0` / `2.0` / `1.5`) were meant for 8 heroes. With 0.3.0 they would be applied on top of the hero-count scaling (x4 HP with 8 heroes). Set `HpMultiplier`, `BossHpMultiplier` and `DamageMultiplier` to `1.0` to get the 0.2.x strength with 8 heroes and less with 5-7.

Saved values in the `.cfg` file override the defaults above. Restart the game after changing them (BepInEx does not reload plugins or config while the game runs).

## What is not scaled

- **Damage over time and curses** that enemies put on heroes (burn, bleed, poison, and so on). Their damage comes from the number of charges on the hero, not from the enemy's attack, so it is unchanged. Enemies that apply more charges are unchanged too.
- **Extra damage from curse stacks on the target** (`IncreasedCursedDamagePerStack`) is added after the multiplier, so it is not multiplied.
- **Enemy damage from items, traits and pets** has no card, so it always uses `DamageMultiplier`, never `AoeDamageMultiplier`.
- **Thorns and other reactive damage** from enemy auras, and any damage that does not go through `Character.DamageWithCharacterBonus`.
- **Enemy shields/block, healing, speed and status effects** are unchanged.
- Enemy cards that damage the enemy itself are multiplied too (same code path); this is rare.
- After **resuming a co-op/solo combat from a save**, a summoned enemy is no longer marked as a summon (the game does not save that flag). With `ScaleSummons = false`, such a summon deals scaled damage after the resume. Its HP is the saved HP either way.

## Co-op

Every player's game simulates the combat itself and the game checks that all players agree, so **every player needs the mod installed, and everyone must use the same multipliers**.

The mod handles the second part for you: **the host's settings win**. When the host creates the room, the mod stores the host's settings in the Photon room properties, and the other players' mods use those values instead of their own config. Only the host's `.cfg` matters in co-op.

- If a player without the mod joins, their enemies are not scaled and the game will keep finding mismatches (HP corrections or combat reloads). Everyone should install it.
- If the host does not have the mod (or has a version without this feature), players who do have it turn scaling off, to stay in sync with the host, and log a warning.
- A host on an older version does not publish `AoeDamageMultiplier`; players then scale AoE cards with the host's `DamageMultiplier`, as that version does.
- A host on a version before 0.3.0 does not publish the `[HeroCount]` settings; players then use 0 (no hero-count scaling), as that version does. Every player sees the same team, so the hero count is the same for everyone.
- The settings are published once, when the room is created. Changing the host's config needs a game restart and a new room anyway.

## Logs

`BepInEx/LogOutput.log` gets:

- `Enemy Scaling <version> loaded. Local settings: ...` at startup.
- `Published scaling settings to the co-op room: ...` on the host of a co-op room.
- `Scaling settings in use (<source>: ...; with N heroes: HP x..., ...)` the first time an enemy spawns, and whenever the values or the hero count change. `<source>` is `local config`, `local config (host)` or `host's room settings`.
- One line per enemy that appears: `Enemy <name> (<id>) [boss] [summon]: HP <base> -> <scaled> (x<multiplier>)`. For a combat resumed from a save it adds `(resumed combat: saved HP is restored next)`: the HP shown on screen is the saved HP, which was already scaled when it was saved.

## Install

1. Across the Obelisk with **BepInEx 5.4.x** (Windows: the `win_x64` build).
2. Copy `EnemyScaling.dll` into `BepInEx/plugins/`.
3. Start the game. The settings file is created on first launch.

## How it works (for modders)

| Patch | What |
|---|---|
| `NPC.InitData` Postfix | Multiplies `Hp` and `HpCurrent`. `InitData` always restarts from `NpcData.Hp`, so the game calling it several times for one enemy does not stack the multiplier. |
| `BattleMatch.BattleTeamManager.CreateNPC` Prefix/Finalizer | Marks summons and resumed-combat spawns for the HP patch and the log. |
| `NPCItem.Init` Postfix | Logs one line per enemy that appears on screen. |
| `Character.DamageWithCharacterBonus` Postfix | Multiplies the result when the caster is an enemy (also used for the displayed numbers). Uses the AoE multiplier when the current card's `TargetType` is `Global`. |
| `DamageMechanics.DealDamageToTarget`, `CardRealtimeData.UpdatePrecalculatedDamages` Prefix/Postfix | Record the card being resolved, because `DamageWithCharacterBonus` does not receive it. |
| `NetworkManager.OnCreatedRoom` Postfix | Host publishes its settings as Photon room properties (`es_on`, `es_hp`, `es_bosshp`, `es_dmg`, `es_aoedmg`, `es_summons`, and `es_hp_hero`, `es_bosshp_hero`, `es_dmg_hero`, `es_aoedmg_hero` for the hero-count settings). |

Resumed combats: `MatchManager.FixTOTALCo` recreates each enemy (which runs `InitData`, so it is scaled) and then overwrites `HpCurrent` and `Hp` with the saved values, which were saved already scaled. So HP is neither scaled twice nor lost.

## Building

Needs the .NET SDK. The project targets `netstandard2.1`.

```
dotnet build -c Release
```

It references the game's DLLs (`Assembly-CSharp`, Unity, Photon) and BepInEx/Harmony from the game folder, and copies the result into `BepInEx/plugins/`. The game folder defaults to the Linux Steam path; override it with `-p:GameDir="C:\path\to\Across the Obelisk"`. No game code is included in this project.
