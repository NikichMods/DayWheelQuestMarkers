# Adversarial Negative Interaction Audit — 2026-09-23

Status: **research evidence complete; production runtime unchanged**.

Branch: `research/adversarial-negative-audit`.

## Question

For every interaction occurrence that the exhaustive GK 1.407 weekday-NPC universe currently treats as non-reminder, ask:

> Can a real game state require the player to make a separate visit to this weekday NPC for that interaction?

The audit deliberately attacks previous negative decisions instead of trusting them. It uses the accepted raw interaction universe, task-route snapshot, no-root frontier, navigation snapshot, and the current lifecycle census before considering any new runtime probe.

## Evidence reviewed

Canonical current production evidence:

- `AGENTS.md`;
- `docs/VERIFIED_RUNTIME_DATA.md`;
- `docs/UNIFIED_INTERACTION_1.1.9.md`;
- `docs/EXHAUSTIVE_VALIDATION_HARNESS.md`;
- `validator/baseline-1.1.9.json`;
- `validator/fixtures/lifecycle-paths-1.1.6.tsv` (the accepted lifecycle fixture retained by 1.1.9).

Historical exhaustive research evidence retained on `research/interaction-universe-snapshot`:

- `validator/fixtures/raw-interaction-universe-1.1.6.tsv`;
- `validator/fixtures/live-answer-dispositions-1.1.6.tsv`;
- `validator/fixtures/navigation-paths-1.1.6.tsv`;
- `validator/fixtures/task-routes-1.1.6.tsv`;
- `validator/fixtures/no-root-frontier-1.1.6.tsv`;
- `docs/INTERACTION_UNIVERSE_COVERAGE.md`;
- `docs/RUNTIME_WATCHDOG.md`.

The historical live-disposition table is evidence for the raw 243-occurrence universe, but it is **not** current semantic canon after the accepted 1.1.6 nearest-ancestor lifecycle work. That distinction matters below.

## Negative-class map

The historical Watchdog 0.2 table classified all 243 authored answer occurrences as:

- 88 `REMINDER_TASK_OWNED`;
- 58 `REMINDER_DIALOGUE_OWNER`;
- 67 `NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER`;
- 4 `SAME_VISIT_NON_REMINDER`;
- 6 `SAME_VISIT_DESCENDANT_OF_DIALOGUE_OWNER`;
- 6 `SAME_VISIT_DESCENDANT_OF_TASK_OWNER`;
- 14 `EVENT_INVOKED_NON_REMINDER`;
- 0 unknown.

That table was derived before the runtime watchdog learned how to assign semantic ownership back to a selectable ancestor. The current production lifecycle census is stronger evidence.

### 1. Event-invoked no-root interactions — 14 occurrences

Evidence strength: **strong / direct topology**.

All 14 have no normal player interaction-root navigation path. Probe 0.1.3 established exactly one scripted `CustomEvent` root family for each occurrence:

- Inquisitor `first_meet_under_mountains`: 7 answers;
- Inquisitor `on_came_to_mountain_for_witch_burning`: 2 answers;
- Inquisitor `inquisitor_after_dark_event`: 3 answers;
- Snake `player_back_to_cultist`: 2 answers.

These are choices inside already-running scripted visits/cutscenes, not reasons to initiate a fresh weekday-NPC visit.

Adversarial result: **retain `EVENT_INVOKED_NON_REMINDER` for all 14**.

No new probe is required.

### 2. Same-visit suppression

Evidence strength: **strong / concrete authored path + persistent effects**.

Current lifecycle census contains:

- 6 path records suppressed as `SUPPRESS_SAME_VISIT`;
- 6 descendants owned by admitted dialogue ancestors;
- 6 descendants owned by task-owned ancestors.

The exact `SUPPRESS_SAME_VISIT` rows are the Astrologer diary follow-up tree:

- `astrologer_diary_9a`;
- `astrologer_diary_9b`;
- `@astrologer_about_acid` through either diary branch;
- `@astrologer_about_tools` through either diary branch.

They have `independentOwnerRoot=False` in the accepted lifecycle evidence: they exist only after entering the already-owned visit and therefore cannot independently justify another weekday reminder.

The descendant classes are likewise not separate visits: the persistent effect consumes the nearest selectable ancestor that owns the visit.

Adversarial result: **retain all same-visit suppressions**.

### 3. Exact task-owned lifecycle suppressions

Evidence strength: **strong / task completion ownership**.

The accepted non-`@` lifecycle census has exactly 9 task/completion exclusions:

- Astrologer: `astrologer_2b`;
- Inquisitor: `inquisitor_dark_brain`, `inquisitor_dark_hart`, `inquisitor_dark_intestine`;
- Merchant: `merchant_2e_1a`, `merchant_2e_1b`, `merchant_2e_1c`;
- Ms Charm: `actress_2a_2`;
- Bishop: `bishop_2_1c`.

These are not negative product decisions. They are deduplication: the visit is already represented by a task-owned contributor.

Adversarial result: **retain all 9 task/completion exclusions**.

### 4. Ancestor task exclusions

Evidence strength: **strong / nearest persistent owner + task ownership**.

The complete ancestor-owner census has exactly six unique candidates. Two are task-owned and therefore excluded only from the generic dialogue layer:

- Merchant `@merchant_2b`;
- Merchant `@merchant_favore_done`.

They still represent reminder semantics when their owning task route is actionable; they are not utility interactions.

Adversarial result: **retain task-owned suppression, but never classify these owners as semantic non-reminders**.

### 5. Historical `NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER` — discovered stale negatives

Evidence strength of the historical label: **insufficient for five occurrences**.

The historical Watchdog 0.2 derivation used a fallback:

`no exact lifecycle row + no direct task completion at this exact occurrence -> NAVIGATION_UTILITY_REPEATABLE_NON_REMINDER`

That fallback does not assign an admitted nearest-ancestor owner back to the ancestor's own menu occurrence. The current lifecycle census proves five historical utility labels are stale:

1. Astrologer main menu `@tr_quest_13_research_1`
   - current evidence: admitted nearest persistent dialogue owner;
   - descendants consume this ancestor;
   - semantic class: reminder dialogue owner.

2. Snake main menu `@snake_1с`
   - current evidence: admitted nearest persistent dialogue owner;
   - descendants `snake_1с_4a` / `snake_1с_4b` consume this ancestor;
   - semantic class: reminder dialogue owner.

3. Merchant submenu `@merchant_2e_1e`
   - current evidence: admitted nearest persistent dialogue owner;
   - descendant `merchant_2e_1d_4a` consumes this ancestor;
   - semantic class: reminder dialogue owner.

4. Bishop main menu `bishop_2_1a`
   - current evidence: admitted nearest persistent dialogue owner;
   - descendants `bishop_2_1a_6a` / `bishop_2_1a_6b` consume this ancestor;
   - semantic class: reminder dialogue owner.

5. Merchant main menu `@merchant_2b`
   - current evidence: nearest persistent ancestor owner, but task-owned;
   - semantic class: task-owned reminder owner / dialogue-layer suppression;
   - it is not a utility/non-reminder.

This is the same failure mode that originally made `@snake_1с` look like navigation-only UI: the negative decision was made at the owner occurrence without propagating descendant lifecycle evidence back to it.

### 6. Remaining historical utility set

After removing the five stale negatives above, **62 historical utility occurrences remain**.

Their evidence is materially stronger than the stale five:

- none is one of the six finite ancestor-owner candidates in the accepted complete lifecycle census;
- none has a direct selectable task-completion route in the complete 70-selectable task snapshot;
- none belongs to the 14 no-root event frontier;
- none is an exact persistent self-owner admitted by the lifecycle census;
- the accepted navigation census has no unsupported paths.

This rules out the known ways an authored answer in the bounded six-NPC universe becomes an independently actionable visit:

- direct task ownership;
- exact persistent dialogue ownership;
- nearest persistent ancestor ownership;
- verified mandatory event-only stage.

The remaining rows are therefore supported as repeatable navigation/utility/non-owning choices under the current evidence model.

Adversarial result: **retain the remaining 62 as non-reminders**.

## Corrected semantic accounting for Watchdog 0.3

The old Watchdog 0.2 disposition table must not be copied unchanged into 0.3.

At minimum, the five stale owner occurrences above must be reclassified from the semantic-negative bucket:

- four -> reminder dialogue owner;
- one -> task-owned reminder owner/suppression.

This audit does not require changing production 1.1.9: production already contains the accepted nearest-ancestor lifecycle model and its four admitted ancestor topics plus the two task-owned ancestor suppressions.

The defect is in the **research watchdog's semantic accounting**, not in accepted production behavior.

## Production conclusion

No new production false-negative hole was found after applying the current 1.1.9 lifecycle evidence.

The adversarial audit found one research/tooling hole:

> Watchdog 0.2's exact live-answer disposition table is stale for five ancestor-owner occurrences and therefore cannot be the semantic oracle for Watchdog 0.3.

All other reviewed negative classes have a direct evidence reason that excludes a separate weekday visit.

## Runtime-test decision

**No player runtime test is required for Point 1.**

Reason:

- the newly found issue is a contradiction between two already accepted repository evidence sets;
- the stronger/current lifecycle census already contains the required proof;
- no unknown runtime topology remains in the audited negative classes;
- no production code changed.

A new runtime diagnostic would duplicate evidence rather than resolve an uncertainty.

## Next step

Point 1 is complete.

Point 2 should build **Runtime Watchdog 0.3** from the frozen 0.2.0 research state, but update its invariant from “every rendered occurrence has any accepted disposition” to the stronger semantic obligation:

`live rendered + pickable -> accepted semantic disposition -> reminder owner (when applicable) -> production contributor obligation`

The watchdog must remain research-only and must not duplicate the production classifier wholesale.
