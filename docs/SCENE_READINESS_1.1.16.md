# Current-save player readiness — 1.1.16 candidate

## Scope and evidence gate

The player authorized a narrow readiness fix, preserving all marker logic and testing A -> A -> B -> A. Stable remains 1.1.15 until explicit acceptance.

Gate: **READY**, before production mutation. The reported stale-WorldObject failure is proved by diagnostic 0.1.0; exact artifact/log evidence is in `TEST_BUILD_LOG.md` and `VERIFIED_RUNTIME_DATA.md`.

- Observable property: do not prewarm against the previous scene while loading a newly deserialized save.
- Canonical owner: native SaveSlotsMenuGUI.StartPlayingGame startup chain and its current-save player inventory assignment.
- Final writer: GK 1.407 `SaveSlotsMenuGUI.<StartPlayingGame>b__15_7` calls `MainGame.me.save.GetSavedPlayerInventory()` followed by `PlayerComponent.SpawnPlayer(true, inventory)` after world restoration, zone setup and InitAllWorldWGOs. SpawnPlayer installs MainGame.player and calls WorldGameObject.RestoreSavedInventory with the same Item; RestoreSavedInventory directly assigns `_data`. GameSave.GetSavedPlayerInventory directly returns `_inventory`.
- Narrow mechanism: before existing six-NPC/graph checks, require non-null GameSave._inventory to be the exact same object as live player's `_data`. Read the fields, not the potentially initializing data property. Old player/old inventory cannot satisfy a newly deserialized save's ownership.
- Blast radius: LoadingCachePrewarmGate has one caller, TryPrewarmDuringLoading. No gameplay actionability, marker emission, manifest schema or fallback behavior is changed.
- Preserved invariants: existing loading flags and all six graph checks remain required; actual player/SmartRes/NPC bindings are still built by the existing loader; no artificial delay, new hook, cache reset, save mutation or gameplay graph parse is added.
- Acceptance: compiled predicate tests cover old/current player, missing/null state, same-save reload and cross-save transitions; existing 103-check universe and dialogue policy remain unchanged. Installed A -> A -> B -> A must prewarm after restoration, reach Ready without the diagnosed final-bind recovery warnings and preserve expected markers. Preserve the existing manifest outside its active path before the test: initial A then proves cold bootstrap behind loading, and the remaining A -> B -> A loads prove cached reads. The backup permits exact restoration if needed; no save state is changed.

## Choice and limits

A native hook after restore would add a patch dependency/lifecycle to maintain. A progress percentage or fixed delay would guess readiness from timing. The verified native inventory ownership is a read-only semantic condition already produced after restoration and shared by the normal startup chain; it is sufficient to reject the observed old-scene window without recreating bindings or weakening checks.

Native IL proves the assignment path; deterministic tests prove the ownership predicate. They do not establish installed scheduling, other-mod compatibility, or new-cache bootstrap on their own. Preserve the existing safe recovery if an unexpected later invalidation still occurs. No diagnostic logging ships in this candidate.
