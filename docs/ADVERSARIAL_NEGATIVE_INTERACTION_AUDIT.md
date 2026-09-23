# Adversarial Negative Interaction Audit — 2026-09-23

Status: **Point 1 complete; research evidence only; production runtime unchanged**.

Branch: `research/adversarial-negative-audit`.

## Question

For every interaction occurrence that the exhaustive Graveyard Keeper 1.407 weekday-NPC universe previously treated as a non-reminder, ask:

> Can a real game state require the player to make a separate visit to this weekday NPC for that interaction?

This audit deliberately attacks previous negative decisions instead of trusting them. It uses existing accepted exhaustive fixtures, historical authored-graph probes, and current 1.1.9 lifecycle/task behavior. No new production code or runtime probe is introduced.

## Evidence reviewed

Current production/canonical evidence:

- `AGENTS.md`;
- `docs/VERIFIED_RUNTIME_DATA.md`;
- `docs/UNIFIED_INTERACTION_1.1.9.md`;
- `docs/EXHAUSTIVE_VALIDATION_HARNESS.md`;
- `docs/MIGRATION_PROVENANCE.md`;
- `validator/baseline-1.1.9.json`;
- accepted 1.1.9 source at `73b35a3bffcb440bf644dd03532fbf2cf6ce4b11`.

Historical exhaustive evidence retained on `research/interaction-universe-snapshot`:

- `validator/fixtures/raw-interaction-universe-1.1.6.tsv`;
- `validator/fixtures/live-answer-dispositions-1.1.6.tsv`;
- `validator/fixtures/navigation-paths-1.1.6.tsv`;
- `validator/fixtures/task-routes-1.1.6.tsv`;
- `validator/fixtures/no-root-frontier-1.1.6.tsv`;
- `validator/fixtures/lifecycle-paths-1.1.6.tsv`;
- `docs/INTERACTION_UNIVERSE_COVERAGE.md`;
- `docs/RUNTIME_WATCHDOG.md`.

Additional archived authored-graph evidence reviewed:

- strict action-chain probe 0.1.24;
- persisted-topic audit 0.1.25;
- topic-provenance audits 0.1.26–0.1.29;
- archived runtime logs used by the earlier intermediate-progression research.

## Historical negative-class map

Watchdog 0.2 classified the 243 authored answer occurrences as:

- 88 `REMINDER_TASK_OWNED`;
- 58 `REMINDER_DIALOGUE_OWNER`;
- 67 `NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER`;
- 4 `SAME_VISIT_NON_REMINDER`;
- 6 `SAME_VISIT_DESCENDANT_OF_DIALOGUE_OWNER`;
- 6 `SAME_VISIT_DESCENDANT_OF_TASK_OWNER`;
- 14 `EVENT_INVOKED_NON_REMINDER`;
- 0 unknown.

Therefore the old table contained 97 negative occurrences.

The central adversarial finding is that `UNKNOWN=0` did **not** prove that those 97 semantic decisions were correct. The old disposition table was a useful exhaustive occurrence inventory, but it was not a safe semantic oracle.

## Why Watchdog 0.2's semantic oracle was wrong

Three independent weaknesses were found.

### 1. Ancestor-owner projection was missing

The later accepted lifecycle model can assign the semantic visit to the nearest persistently consumed selectable ancestor.

The old live-disposition table classified the individual rendered occurrence. It did not project descendant lifecycle evidence back onto the ancestor's own menu occurrence.

That made real semantic owners such as `@snake_1с` look like ordinary navigation entries.

### 2. Exact answer IDs were not preserved across one fixture join

The raw interaction universe contains the literal answer ID:

`@actress_ jewelry`

with an embedded space.

The lifecycle snapshot represented the same owner as:

`@actress__jewelry`

The exact join therefore failed even though authored evidence proves that `@actress_ jewelry` is a gated, self-consuming one-time topic.

Watchdog 0.3 must preserve authored answer IDs exactly. Normalized/log-safe spellings must never become semantic keys.

### 3. The compact task-route snapshot stored a representative answer, not every equivalent answer variant

For a completion node with several incoming answer variants, the compact snapshot could retain one representative answer even though multiple siblings converge on the same completion topology.

Examples include:

- `inquisitor_dark_hart` / `inquisitor_dark_brain` / `inquisitor_dark_intestine`;
- `merchant_2e_1a` / `merchant_2e_1b` / `merchant_2e_1c`;
- the singing-charm answer family around `tr_quest_8_singing_charm_7a/7b/7c`.

Current production does not depend on that one-row simplification: its task compiler enumerates authored anchors around the completion topology.

Watchdog 0.3 must likewise avoid treating absence from the compact representative table as proof of non-reminder semantics.

## Corrected audit of the 67 historical utility occurrences

The full 67-row historical `NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER` set partitions cleanly as:

- **6 stale negatives that are actually semantic reminder owners**;
- **8 same-visit descendants**;
- **7 progression starters that create the next objective only after selection**;
- **4 relationship/magic follow-up topics with no independent progression ownership**;
- **42 genuine menu/navigation/no-progress occurrences**.

Total: **67 / 67 classified**.

### A. Six stale negatives — semantic reminder owners

These six must not remain in the semantic-negative bucket:

1. Ms Charm — `@actress_ jewelry`
   - exact authored ID contains a space;
   - requires `bijouterie_gold=1`;
   - persistently blacklists itself;
   - activates the Merchant handoff;
   - semantic class: dialogue-lifecycle owner.

2. Astrologer — `@tr_quest_13_research_1`
   - nearest persistently consumed selectable ancestor;
   - semantic class: dialogue-lifecycle owner.

3. Bishop — `bishop_2_1a`
   - nearest persistently consumed selectable ancestor;
   - descendants `bishop_2_1a_6a/6b` consume the visit owner;
   - semantic class: dialogue-lifecycle owner.

4. Snake — `@snake_1с`
   - nearest persistently consumed selectable ancestor;
   - descendants `snake_1с_4a/4b` consume the visit owner;
   - semantic class: dialogue-lifecycle owner.

5. Merchant — `@merchant_2b`
   - not utility;
   - authored effect completes the cross-owner `horadric_garden` task route;
   - semantic class: task-owned reminder owner, suppressed only from the generic dialogue layer.

6. Merchant — `@merchant_2e_1e`
   - nearest persistently consumed selectable ancestor;
   - descendant `merchant_2e_1d_4a` consumes the visit owner;
   - semantic class: dialogue-lifecycle owner.

Important production result:

**All six are already represented by the accepted 1.1.9 production architecture.**

The audit therefore found a research-oracle defect, not a new stable-runtime false negative.

### B. Eight same-visit descendants — remain non-reminders

These are actionable only after the player has already entered the visit that owns the reminder:

- Ms Charm:
  - `tr_quest_8_singing_charm_7b`;
  - `tr_quest_8_singing_charm_7c`.
- Bishop:
  - `bishop_2_1e_4a`;
  - `bishop_2_1e_4b`;
  - `bishop_2_1e_4c`.
- Snake:
  - `snake_aple_get_3a`;
  - `snake_aple_get_4b`.
- Merchant:
  - `merchant_2e_1d_4b`.

The singing-charm alternatives are especially important: authored action-chain evidence reaches the same progression topology, but the choices live under the already-entered `@tr_quest_8_singing_charm_1` conversation owner. They do not justify a second weekday reminder.

Adversarial result: **retain all eight as same-visit non-reminders**.

### C. Seven progression starters — remain non-reminders until after selection

These choices create or expose the next task/objective. The new objective is not active before the player selects them:

- Bishop:
  - `bishop_2_1e`.
- Inquisitor:
  - `inquisitor_burn_3a_1a`;
  - `inquisitor_burn_3a_1b`.
- Merchant:
  - `merchant_on_deal_done_4b`;
  - `merchant_70_7a` at multi 1121;
  - `merchant_70_7a` at multi 1126;
  - `merchant_70_7b_2b` at multi 1126.

Examples from the authored effects:

- the Merchant 70 branch creates `merchant_support Visible` and activates the Ms Charm handoff;
- `merchant_on_deal_done_4b` creates `merchant_trade Visible`;
- the Inquisitor burn choices advance into the subsequent task state.

These are progression transitions, not pre-existing “come back to this NPC” obligations.

Adversarial result: **retain all seven as non-reminders at the pre-selection state**.

### D. Four `*_magic_100` follow-up topics — remain non-reminders

- Bishop — `@bishop_magic_100`;
- Snake — `@snake_magic_100`;
- Inquisitor — `@inquisitor_magic_100`;
- Merchant — `@merchant_magic_100`.

The preceding `*_magic_item` interaction is the one-time self-consuming transition that activates the corresponding `*_magic_100` topic.

The `*_magic_100` rows themselves:

- are gated by relationship 100;
- do not complete/create a task;
- do not activate another progression topic;
- do not persistently consume themselves;
- behave as relationship follow-up/menu content.

Adversarial result: **retain all four as non-reminders**.

### E. Forty-two genuine menu/navigation/no-progress occurrences

These have no independent task ownership, no admitted lifecycle ownership, no verified event-only visit ownership, and no authored progression effect that makes them a separate weekday obligation.

#### Ms Charm — 4

- `actress_2b`;
- `@actress_trade`;
- `Leave`;
- `actress_no_question`.

#### Astrologer — 7

- `astrologer_2a`;
- `@astrologer_trade`;
- `Leave`;
- `@astrologer_2a_1b_1`;
- `Back` at multi 67;
- `Back` at multi 104;
- `Leave` at multi 1384.

#### Bishop — 9

- `bishop_2_1b`;
- `@bishop_trade` at multi 75;
- `bishop_2_1d`;
- `@bishop_trade` at multi 679;
- `Leave` at multi 679;
- `about_cathedral`;
- `Trade`;
- `Leave` at multi 1030;
- `Back` at multi 1068.

#### Snake — 6

- `@snake_about_nacklase`;
- `@snake_ritual_help`;
- `Leave` at multi 106;
- `Leave` at multi 763;
- `Leave` at multi 985;
- `Leave` at multi 1520.

#### Inquisitor — 5

- `Leave` at multi 736;
- `@inquisitor_burn_again`;
- `@inquisitor_dark`;
- `Leave` at multi 843;
- `Back` at multi 1556.

#### Merchant — 11

- `@merchant_2e`;
- `@merchant_business`;
- `merchant_2c`;
- `@merchant_new_trade`;
- `merchant_2d`;
- `merchant_2b_5b`;
- `merchant_2b_5b_2b`;
- `merchant_2e_1d`;
- `merchant_2e_1g`;
- `merchant_business_back`;
- `merchant_70_7b`.

Adversarial result: **retain all 42 as genuine utility/navigation/no-progress occurrences**.

## Other historical negative classes

### Event-invoked no-root interactions — 14 occurrences

Evidence strength: **strong / direct topology**.

All 14 lack a normal player interaction-root navigation path. Existing no-root research maps them to already-running scripted event/cutscene families:

- Inquisitor `first_meet_under_mountains`: 7 answers;
- Inquisitor `on_came_to_mountain_for_witch_burning`: 2 answers;
- Inquisitor `inquisitor_after_dark_event`: 3 answers;
- Snake `player_back_to_cultist`: 2 answers.

These are choices made after the scripted visit has already started, not reasons to initiate another weekday visit.

Adversarial result: **retain all 14 as event-invoked non-reminders**.

### Explicit same-visit classes — 16 occurrences

Historical table:

- 4 `SAME_VISIT_NON_REMINDER`;
- 6 `SAME_VISIT_DESCENDANT_OF_DIALOGUE_OWNER`;
- 6 `SAME_VISIT_DESCENDANT_OF_TASK_OWNER`.

The four direct same-visit rows are the Astrologer diary follow-up family:

- `astrologer_diary_9a`;
- `astrologer_diary_9b`;
- `@astrologer_about_acid`;
- `@astrologer_about_tools`.

The accepted lifecycle evidence gives them no independent owner root.

The six dialogue-owner descendants are:

- `tr_quest_13_research_2` under `@tr_quest_13_research_1`;
- `bishop_2_1a_6a/6b` under `bishop_2_1a`;
- `snake_1с_4a/4b` under `@snake_1с`;
- `merchant_2e_1d_4a` under `@merchant_2e_1e`.

The six task-owner descendants are:

- `merchant_2b_5a`;
- `merchant_2b_5b_2a`;
- `merchant_2b_5b_2b_4a`;
- `merchant_2b_5b_2b_4b`;
- `merchant_favore_done_12a`;
- `merchant_favore_done_12b`.

Adversarial result: **retain all 16 as same-visit suppressions**.

## Task-layer suppressions and completion exclusions

These cases are important because “suppressed from dialogue lifecycle” must not be mistaken for “semantic non-reminder”.

### Nine non-`@` completion exclusions

Current lifecycle evidence has exactly nine task-owned non-`@` self candidates:

- Astrologer — `astrologer_2b`;
- Inquisitor — `inquisitor_dark_brain`;
- Inquisitor — `inquisitor_dark_hart`;
- Inquisitor — `inquisitor_dark_intestine`;
- Merchant — `merchant_2e_1a`;
- Merchant — `merchant_2e_1b`;
- Merchant — `merchant_2e_1c`;
- Ms Charm — `actress_2a_2`;
- Bishop — `bishop_2_1c`.

For the sibling Inquisitor-dark and Merchant-crop choices, the compact task-route fixture stores one representative completion answer, but the raw authored topology shows the sibling choices converging on the same task-completion node. Production's task compiler enumerates the authored completion anchors instead of relying on the representative fixture row.

Adversarial result: **retain all nine dialogue-layer completion exclusions; the task layer owns the reminder semantics**.

### Two task-owned ancestor owners

The complete ancestor-owner census has six unique ancestor candidates. Four are admitted dialogue owners and two are task-owned suppressions:

- Merchant `@merchant_2b`;
- Merchant `@merchant_favore_done`.

Independent authored-effect evidence confirms both task relationships:

- `@merchant_2b` completes the cross-owner `horadric_garden` route;
- `@merchant_favore_done` completes `merchant_grass`, creates `merchant_curse Visible`, activates the Clotho handoff, and consumes itself.

Adversarial result: **retain task-layer suppression; neither owner is a semantic non-reminder**.

## Corrected semantic accounting

Old Watchdog 0.2 negative count:

- 67 utility;
- 4 same-visit;
- 6 dialogue-owner descendants;
- 6 task-owner descendants;
- 14 event-invoked;
- total = **97 negative occurrences**.

This audit moves six stale utility rows into semantic reminder ownership.

Corrected result:

- 6 stale negatives -> reminder semantics already represented by production;
- 61 true negatives remain from the historical utility bucket;
- the other 30 historical negative rows remain negative;
- total supported semantic negatives = **91 occurrences**;
- unresolved frontier = **0** within the audited 243-occurrence universe.

This does **not** redefine production around a fixed 91-row hardcoded list. The count is audit evidence for the current authored universe, not a shipping classification table.

## Production conclusion

**No new production false-negative hole was found in Point 1.**

The accepted 1.1.9 architecture already represents all six occurrences that the historical Watchdog 0.2 table had wrongly put in the negative utility bucket.

Therefore:

- no production source change is justified;
- no version bump is justified;
- no DLL is produced;
- no hosted CI run is justified;
- stable `main` remains untouched.

The concrete defect discovered by Point 1 is in the **research semantic oracle** used by Watchdog 0.2.

## Canonical consequence for Watchdog 0.3

Watchdog 0.3 must **not** copy `live-answer-dispositions-1.1.6.tsv` as a semantic truth table.

Its independent runtime invariant should be:

`live rendered + pickable interaction`
-> accepted semantic disposition
-> semantic reminder owner when applicable
-> production contributor obligation.

Implementation constraints established by this audit:

1. preserve authored answer IDs exactly, including spaces and non-ASCII characters;
2. project admitted descendant lifecycle evidence back to its semantic selectable owner;
3. distinguish task-layer suppression from semantic non-reminder;
4. do not infer “non-reminder” merely because an exact occurrence is absent from the compact representative task-route snapshot;
5. keep event-invoked and same-visit cases explicitly explainable;
6. remain research-only and avoid reimplementing the production classifier wholesale.

## Runtime-test decision

**No player runtime test is required for Point 1.**

Existing repository and archived authored-graph/runtime evidence is sufficient to close every negative frontier examined here. A new probe would duplicate established evidence rather than resolve an uncertainty.

## Next step

**Point 1 complete. Next: Point 2 — Runtime Watchdog 0.3.**
