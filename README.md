# Enemy Scaling

A BepInEx 5 mod for **Across the Obelisk** that makes enemies tougher: it multiplies enemy HP and enemy card damage by configurable factors. It works in the normal 4-hero game, solo or co-op. (It is also the first step of a larger "8 heroes" project, where enemies need to scale up for a bigger party.)

**Status:** builds and is based on reading the game code; not yet tested in game. Back up your save before using it on a run you care about.

## What it does

- **Enemy HP** is multiplied when each enemy is created, after the game's own NG+, challenge-trait and sandbox HP changes. The HP bar shows the new max HP. Bosses (`NpcData.IsBoss`) use their own multiplier.
- **Enemy damage** is multiplied after all of the enemy's own damage bonuses (NG+, traits, items, sandbox). The numbers shown on enemy cards and in attack previews include the multiplier, because the game computes them with the same method.
- **Summoned enemies** (minions created during combat) are scaled too, unless `ScaleSummons` is off. An enemy that transforms (metamorph/evolve) is treated as the same enemy and always scaled.
- Results are rounded to whole numbers and never drop below 1.

## Settings (`BepInEx/config/com.cojoco.enemyscaling.cfg`)

| Section / key | Default | Meaning |
|---|---|---|
| `[General] Enabled` | `true` | Turn scaling on or off |
| `[Scaling] HpMultiplier` | `2.0` | Max HP multiplier for enemies that are not bosses |
| `[Scaling] BossHpMultiplier` | `2.0` | Max HP multiplier for bosses (set it equal to `HpMultiplier` to treat bosses the same) |
| `[Scaling] DamageMultiplier` | `1.5` | Enemy damage multiplier |
| `[Scaling] ScaleSummons` | `true` | Also scale HP and damage of enemies summoned mid-combat |

Saved values in the `.cfg` file override the defaults above. Restart the game after changing them (BepInEx does not reload plugins or config while the game runs).

## What is not scaled

- **Damage over time and curses** that enemies put on heroes (burn, bleed, poison, and so on). Their damage comes from the number of charges on the hero, not from the enemy's attack, so it is unchanged. Enemies that apply more charges are unchanged too.
- **Extra damage from curse stacks on the target** (`IncreasedCursedDamagePerStack`) is added after the multiplier, so it is not multiplied.
- **Thorns and other reactive damage** from enemy auras, and any damage that does not go through `Character.DamageWithCharacterBonus`.
- **Enemy shields/block, healing, speed and status effects** are unchanged.
- Enemy cards that damage the enemy itself are multiplied too (same code path); this is rare.
- After **resuming a co-op/solo combat from a save**, a summoned enemy is no longer marked as a summon (the game does not save that flag). With `ScaleSummons = false`, such a summon deals scaled damage after the resume. Its HP is the saved HP either way.

## Co-op

Every player's game simulates the combat itself and the game checks that all players agree, so **every player needs the mod installed, and everyone must use the same multipliers**.

The mod handles the second part for you: **the host's settings win**. When the host creates the room, the mod stores the host's settings in the Photon room properties, and the other players' mods use those values instead of their own config. Only the host's `.cfg` matters in co-op.

- If a player without the mod joins, their enemies are not scaled and the game will keep finding mismatches (HP corrections or combat reloads). Everyone should install it.
- If the host does not have the mod (or has a version without this feature), players who do have it turn scaling off, to stay in sync with the host, and log a warning.
- The settings are published once, when the room is created. Changing the host's config needs a game restart and a new room anyway.

## Logs

`BepInEx/LogOutput.log` gets:

- `Enemy Scaling 0.1.0 loaded. Local settings: ...` at startup.
- `Published scaling settings to the co-op room: ...` on the host of a co-op room.
- `Scaling settings in use (<source>: ...)` the first time an enemy spawns, and whenever the values change. `<source>` is `local config`, `local config (host)` or `host's room settings`.
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
| `Character.DamageWithCharacterBonus` Postfix | Multiplies the result when the caster is an enemy (also used for the displayed numbers). |
| `NetworkManager.OnCreatedRoom` Postfix | Host publishes its settings as Photon room properties (`es_on`, `es_hp`, `es_bosshp`, `es_dmg`, `es_summons`). |

Resumed combats: `MatchManager.FixTOTALCo` recreates each enemy (which runs `InitData`, so it is scaled) and then overwrites `HpCurrent` and `Hp` with the saved values, which were saved already scaled. So HP is neither scaled twice nor lost.

## Building

Needs the .NET SDK. The project targets `netstandard2.1`.

```
dotnet build -c Release
```

It references the game's DLLs (`Assembly-CSharp`, Unity, Photon) and BepInEx/Harmony from the game folder, and copies the result into `BepInEx/plugins/`. The game folder defaults to the Linux Steam path; override it with `-p:GameDir="C:\path\to\Across the Obelisk"`. No game code is included in this project.
