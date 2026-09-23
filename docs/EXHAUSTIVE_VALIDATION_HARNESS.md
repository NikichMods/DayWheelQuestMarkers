# Exhaustive Interaction Validation Harness

Status: **accepted validation tooling, current baseline 1.1.9**.

This harness exists to reduce dependence on full manual Graveyard Keeper playthroughs after structural reminder changes.

## Goal

For Graveyard Keeper 1.407, keep a compact accepted evidence baseline for the six weekday NPCs and automatically reject unexplained semantic drift in Day Wheel Quest Markers.

The harness is deliberately separate from the public runtime DLL. It does not mutate saves, render markers, or run during normal gameplay.

## Current automatic validator

Entry point:

`python validator/validate_interaction_universe.py`

CI:

`.github/workflows/validate-interaction-universe.yml`

The workflow runs automatically on pull requests that change interaction-semantic production files or validator files, and is also available through `workflow_dispatch`.

### Independent lifecycle oracle

The retained accepted 1.1.6 read-only lifecycle census produced **216 path records** and remains the independent lifecycle oracle for 1.1.9:

- 204 self-owned path instances;
- 12 ancestor-owned path instances;
- 117 task-owned suppressions;
- 6 same-visit suppressions;
- 93 admitted paths;
- 60 unique admitted dialogue owners.

The committed fixture stores lower-level path facts:

- NPC;
- selected branch;
- concrete selectable root path;
- persistent blacklist effects;
- task-owned flag;
- independent-root flag;
- reversible flag.

The Python validator does **not** trust the recorded owner/disposition. It independently derives:

1. exact self owner if the branch consumes itself;
2. otherwise nearest consumed selectable ancestor on the same path;
3. suppression for reversible/task-owned/non-independent cases;
4. admission otherwise.

It then compares its result with the accepted census. This catches accidental changes to the semantic model without executing production C#.

Control cases include:

- Snake `@snake_1с` -> admitted ancestor owner;
- Merchant `@merchant_2b` -> task-owned suppression;
- Merchant `@merchant_2e_1e` -> admitted ancestor owner;
- Merchant `@merchant_favore_done` -> task-owned suppression;
- Astrologer diary `9a/9b` -> same-visit suppression.

### Owner-task census

The accepted independent task audit is also stored compactly.

It proves the complete owner-local completion universe:

- **72** owner-local `Complete` nodes;
- probe 0.1.1 resolved **68** to selectable answers;
- **4** required additional topology evidence;
- event-provenance research recovered two more selectable routes:
  - `snake_key -> @snake_give_key`;
  - `snake_trap -> snake_stone_ready`;
- the remaining two are genuine mandatory event-only stages:
  - `npc_inquisitor/inquisitor_talk`;
  - `npc_cultist/snake_back`.

Final partition:

`72 = 70 selectable task completions + 2 event-only stages`

The validator recomputes the census arithmetic and checks that production's exact event-only set and verified completion supplement still match the accepted evidence.

### Production contract checks

The validator also checks current source for:

- schema version;
- owner/cross/base-topic canonical counts;
- lifecycle-universe guard constants;
- self-first and nearest-ancestor semantic guards;
- task-owned/navigation/reversible suppressions;
- exact promoted completion routes;
- exact event-only whitelist;
- exact `snake_trap` relation-gated route;
- absence of the retired 1.1.5 Snake fake-coins hard-code;
- manifest use of `UnifiedDialogueLifecycleCompiler.Validate`.

These checks are intentionally bounded. They are not a substitute for executing the game.

## First CI result

Workflow run `35449940018`:

- result: **PASS**;
- **52 checks / 0 failed**;
- lifecycle: **216 path records / 60 unique admitted owners**;
- tasks: **72 completion nodes -> 70 selectable + 2 event-only**;
- report artifact: `interaction-universe-report`.

## Closed coverage layers

The former per-route owner-task and standalone-navigation fixture gaps are closed by the accepted interaction-universe snapshot:

- **72/72** owner completion routes are classified: 70 selectable + 2 event-only;
- **270/270** authored navigation paths are stored in the production-derived navigation fixture;
- the raw six-NPC universe contains **243 authored answer occurrences** with exact accepted dispositions and **UNKNOWN=0**;
- the 14 no-normal-root occurrences are independently classified as event-invoked non-reminders;
- relay-backed `MultipleAnswerData` has an exact **7-use** fixture covering Inquisitor 2, Snake 1, Merchant 1, Bishop 3;
- the complete compound family has runtime-proven native AND semantics.

The lifecycle fixture remains an independent oracle rather than being replaced by production-derived navigation data.

## Current accepted 1.1.9 validator result

Exact accepted build-bearing source `73b35a3bffcb440bf644dd03532fbf2cf6ce4b11` was validated in workflow run `35894904798`, job `107296291101`:

- result: **PASS**;
- **68 checks / 0 failed**;
- lifecycle: **216 path records / 60 unique admitted owners**;
- tasks: **72 completion nodes -> 70 selectable + 2 event-only**;
- `MultipleAnswerData`: **7 verified menu uses across 4 weekday NPCs**;
- canonical schema-6 owner partition: **81 supported / 0 unsupported**.

## What the validator proves

Once a structural change enters a PR, the automatic validator can catch unexplained changes to the accepted interaction universe before a player build is accepted.

It is strongest for dialogue lifecycle classification because that layer already has complete path-level independent evidence.

It also guards the complete task census partition, complete snapshot/navigation identities, exact live-answer dispositions, the `MultipleAnswerData` usage set, and the exact residual special/event set.

## What it cannot prove by itself

Static CI cannot execute Graveyard Keeper's live runtime:

- `Player.IsEnough(SmartRes)`;
- actual save phrase/task transitions;
- Unity HUD object lifetime;
- rendered marker count;
- host event timing.

Those remain runtime concerns.

The planned second layer is therefore a separate **read-only runtime sentinel**. During ordinary play it should remain invisible while contracts agree and show an unmistakable FAIL indicator only when a detectable invariant is violated. It must not ship in the public production DLL.

## Acceptance policy for future structural changes

A structural interaction change should not be accepted merely because the aggregate counts still match.

Required process:

1. automatic validator passes against the accepted baseline;
2. any intentional baseline delta is explained at the individual interaction level;
3. runtime smoke checks cover only host behavior that static evidence cannot prove;
4. normal playthroughs become exploratory/end-to-end evidence, not the primary regression mechanism.
