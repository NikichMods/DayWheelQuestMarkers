# Current-save player readiness — accepted 1.1.16

## Scope and evidence gate

The player authorized a narrow readiness fix, preserving all marker logic and testing A -> A -> B -> A. The player explicitly accepted 1.1.16 on 2026-10-17 after the loading regression and normal marker visuals were confirmed.

Gate: **READY**, before production mutation. The reported stale-WorldObject failure is proved by diagnostic 0.1.0; exact artifact/log evidence is in `TEST_BUILD_LOG.md` and `VERIFIED_RUNTIME_DATA.md`.

- Observable property: do not prewarm against the previous scene while loading a newly deserialized save.
- Canonical owner: native SaveSlotsMenuGUI.StartPlayingGame startup chain and its current-save player inventory assignment.
- Final writer: GK 1.407 `SaveSlotsMenuGUI.<StartPlayingGame>b__15_7` calls `MainGame.me.save.GetSavedPlayerInventory()` followed by `PlayerComponent.SpawnPlayer(true, inventory)` after world restoration, zone setup and InitAllWorldWGOs. SpawnPlayer installs MainGame.player and calls WorldGameObject.RestoreSavedInventory with the same Item; RestoreSavedInventory directly assigns `_data`. GameSave.GetSavedPlayerInventory directly returns `_inventory`.
- Narrow mechanism: before existing six-NPC/graph checks, require non-null GameSave._inventory to be the exact same object as live player's `_data`. Read the fields, not the potentially initializing data property. Old player/old inventory cannot satisfy a newly deserialized save's ownership.
- Blast radius: LoadingCachePrewarmGate has one caller, TryPrewarmDuringLoading. No gameplay actionability, marker emission, manifest schema or fallback behavior is changed.
- Preserved invariants: existing loading flags and all six graph checks remain required; actual player/SmartRes/NPC bindings are still built by the existing loader; no artificial delay, new hook, cache reset, save mutation or gameplay graph parse is added.
- Acceptance protocol (completed; result below): compiled predicate tests cover old/current player, missing/null state, same-save reload and cross-save transitions; existing 103-check universe and dialogue policy remain unchanged. Installed A -> A -> B -> A must prewarm after restoration, reach Ready without the diagnosed final-bind recovery warnings and preserve expected markers. Preserve the existing manifest outside its active path before the test: initial A then proves cold bootstrap behind loading, and the remaining A -> B -> A loads prove cached reads. The backup permits exact restoration if needed; no save state is changed.

## Choice and limits

A native hook after restore would add a patch dependency/lifecycle to maintain. A progress percentage or fixed delay would guess readiness from timing. The verified native inventory ownership is a read-only semantic condition already produced after restoration and shared by the normal startup chain; it is sufficient to reject the observed old-scene window without recreating bindings or weakening checks.

Native IL proves the assignment path; deterministic tests prove the ownership predicate. They do not establish installed scheduling, other-mod compatibility, or new-cache bootstrap on their own. Preserve the existing safe recovery if an unexpected later invalidation still occurs. No diagnostic logging ships in accepted 1.1.16.

## Installed regression result — 2026-10-17

The exact frozen 1.1.16 DLL passed the player-operated A -> A -> B -> A loading regression. Every prewarm occurred after RestoreScene and GetSavedPlayerInventory and before loading hid; all four loads reached canonical Ready without plugin warnings/errors or final-binding recovery. Cold bootstrap took 885.42 ms; cached reads took 5.50 / 3.14 / 2.82 ms with graph parsing skipped. The regenerated schema-7 manifest is byte-identical to the pre-test accepted manifest. Snapshot/artifact hashes are recorded in TEST_BUILD_LOG.md. This verifies the observed loading defect on the tested sequence. The player subsequently confirmed normal marker visuals and explicitly accepted 1.1.16 on 2026-10-17. Exact tested bytes are the stable payload; no rebuild is used for publication.
