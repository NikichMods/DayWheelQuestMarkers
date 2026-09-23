# Unified Interaction Architecture — accepted 1.1.9

Status: **accepted stable architecture for Graveyard Keeper 1.407**.

This document supersedes `docs/UNIFIED_INTERACTION_1.1.6.md` as the current architecture summary. The 1.1.6 document remains historical evidence for the nearest-lifecycle-owner change.

## Product invariant

`currently actionable weekday-NPC interaction -> NPC weekday -> marker`

A marker means the player can make relevant progress by visiting that weekday NPC now. An unfinished quest alone is not sufficient.

## Evidence classes

Production recognizes three evidence classes:

1. **Task-owned selectable interaction** — a Visible task has an authored completion route through a weekday NPC and every phrase/resource/navigation requirement is currently satisfied.
2. **Dialogue-lifecycle interaction** — an authored branch persistently consumes either the selected answer itself or the nearest consumed selectable ancestor on the same concrete root path.
3. **Verified mandatory event stage** — the interaction has no selectable answer. This remains limited to `npc_inquisitor/inquisitor_talk` and `npc_cultist/snake_back`.

Task-owned and same-visit duplicates are suppressed. Repeatable navigation/utility entries are not reminders.

## Gate model

Direct `Flow_Answer` gates use the game's own SmartRes sufficiency path.

1.1.9 additionally supports the verified compound structure:

`RelayValueOutput<MultipleAnswerData> -> RelayValueInput<MultipleAnswerData> -> Flow_MultipleAnswer -> Flow_AnswersArray -> child Flow_Answer`

The loaded GK 1.407 IL proves that `MultipleAnswerData.FillVisualData` combines every child lock and price as **AND** conditions. Production therefore persists every verified child requirement and requires all of them to pass through the existing game-owned sufficiency path.

Unknown or ambiguous relay ownership, unexpected intermediate nodes, unsupported child gates, and empty child sets fail closed. No Souls quest, NPC, item, or translated display text is hard-coded into this generic support.

The complete accepted compound census is **7 menu uses across 4 weekday NPCs**.

## Navigation and dialogue ownership

Navigation remains path-aware:

- predicates are derived from concrete authored root-to-answer paths;
- predicates within one path are AND;
- alternative authored paths are OR;
- unconditional plain ancestors compile away;
- unsupported or ambiguous ancestry fails closed.

Dialogue ownership remains:

1. exact persistent self-consumption;
2. otherwise nearest persistently consumed selectable ancestor on that same root path;
3. task-owned owners suppressed;
4. same-visit descendants suppressed;
5. reversible/non-independent owners rejected.

The accepted ancestor census remains **6 candidates / 2 task-owned exclusions / 4 admitted / 0 unsupported**.

## Persistent runtime model

`PersistentRuleManifest` schema **6** stores:

- owner-local task rules;
- cross-owner task rules;
- direct and compound SmartRes requirements;
- dialogue-lifecycle topics;
- navigation predicates;
- lifecycle integrity counts.

The six weekday-NPC graphs are parsed only during loading/bootstrap when the schema-6 file is absent or incompatible. Normal gameplay evaluates compact cached rules only.

Accepted integrity counts:

- owner **81 supported / 0 unsupported**;
- cross-owner **8 tasks / 6 supported / 0 unsupported**;
- dialogue-lifecycle **65 / 65 / 0**;
- non-`@` universe **77 / 19 exact-self / 6 admitted / 9 exclusions**;
- ancestor owners **6 / 2 / 4 / 4 / 0**;
- navigation **210 answers / 270 paths / 151 predicates / 0 unsupported**.

## Accepted runtime evidence

Accepted 1.1.9 was built and tested from exact source `73b35a3bffcb440bf644dd03532fbf2cf6ce4b11`.

Runtime evidence:

- schema-5 -> schema-6 bootstrap: **923.35 ms** behind loading;
- persisted schema-6 load: **10.62 ms**, `FlowCanvas graph parse skipped`;
- preserved Snake Better Save Soul state: expected **3 markers** including the new Snake/Envy reminder;
- after selecting `@souls_s_s33_ask`, `dlc_souls_s29_3` completed and the wheel transitioned **3 -> 2** while the other two reminders remained.

The accepted DLL is 101,376 bytes, SHA-256 `069f9e1533f42fb4c4673effb5069de4354d72aeeb32e48818b14a752cb6359e`.

## Deliberate boundaries

The rejected universal provenance parser remains rejected. Production does not recursively walk arbitrary external quest/event dependency chains.

The remaining exact supplement is intentionally narrow. Before adding another exact rule, first test whether the state belongs in the generic task, navigation, dialogue-lifecycle, or compound-gate compiler.

False positives remain more harmful than a known unsupported false negative. Unknown structures fail closed.
