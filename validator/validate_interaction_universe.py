#!/usr/bin/env python3
"""Independent regression validator for the accepted GK 1.407 interaction universe.

This tool intentionally does not execute Graveyard Keeper or call production C# code.
It validates compact evidence captured by accepted read-only runtime censuses and checks
that production source still declares the same bounded contracts. Structural changes
must either preserve this baseline or update it with reviewed evidence.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_BASELINE = ROOT / "validator" / "baseline-1.1.10.json"
DEFAULT_LIFECYCLE = ROOT / "validator" / "fixtures" / "lifecycle-paths-1.1.6.tsv"
DEFAULT_TASKS = ROOT / "validator" / "fixtures" / "task-census-1.1.6.tsv"
DEFAULT_TASK_ROUTES = ROOT / "validator" / "fixtures" / "task-routes-1.1.6.tsv"
DEFAULT_MULTIPLE_ANSWER = ROOT / "validator" / "fixtures" / "multiple-answerdata-1.407.tsv"
DEFAULT_REPORT = ROOT / "validator" / "out" / "validation-report.json"


class CheckLog:
    def __init__(self) -> None:
        self.checks: list[dict] = []
        self.failures: list[str] = []
        self.warnings: list[str] = []

    def check(self, name: str, condition: bool, detail: str) -> None:
        self.checks.append({"name": name, "ok": bool(condition), "detail": detail})
        if not condition:
            self.failures.append(f"{name}: {detail}")

    def warn(self, message: str) -> None:
        self.warnings.append(message)


def load_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def parse_bool(value: str) -> bool:
    if value == "True":
        return True
    if value == "False":
        return False
    raise ValueError(f"expected True/False, got {value!r}")


def load_lifecycle(path: Path) -> list[dict[str, str]]:
    lines = [
        line for line in path.read_text(encoding="utf-8").splitlines()
        if line and not line.startswith("#")
    ]
    if not lines:
        return []
    reader = csv.DictReader(lines, delimiter="\t")
    return list(reader)


def load_tsv(path: Path) -> list[dict[str, str]]:
    lines = [
        line for line in path.read_text(encoding="utf-8").splitlines()
        if line and not line.startswith("#")
    ]
    if not lines:
        return []
    return list(csv.DictReader(lines, delimiter="\t"))


def derive_lifecycle_owner(row: dict[str, str]) -> tuple[str | None, str | None]:
    branch = row["branch"]
    path = row["path"].split(">") if row["path"] else []
    blacklists = {x for x in row["blacklists"].split(",") if x}

    if not path or path[-1] != branch:
        return None, None
    if branch in blacklists:
        return branch, "self"

    for ancestor in reversed(path[:-1]):
        if ancestor in blacklists:
            return ancestor, "ancestor"
    return None, None


def derive_disposition(row: dict[str, str], owner: str | None) -> str:
    if owner is None:
        return "NO_OWNER"
    if parse_bool(row["reversible"]):
        return "SUPPRESS_REVERSIBLE"
    if parse_bool(row["taskOwned"]):
        return "SUPPRESS_TASK_OWNED"
    if not parse_bool(row["independentOwnerRoot"]):
        return "SUPPRESS_SAME_VISIT"
    return "ADMIT"


def validate_lifecycle(rows: list[dict[str, str]], baseline: dict, log: CheckLog) -> dict:
    expected = baseline["lifecycle"]
    log.check("lifecycle.path_count", len(rows) == expected["path_records"],
              f"observed={len(rows)} expected={expected['path_records']}")

    mismatch: list[str] = []
    derived = []
    for row in rows:
        owner, kind = derive_lifecycle_owner(row)
        disposition = derive_disposition(row, owner)
        derived.append((row, owner, kind, disposition))
        if owner != row["acceptedOwner"] or kind != row["acceptedOwnerKind"] or disposition != row["acceptedDisposition"]:
            mismatch.append(
                f"{row['npc']}:{row['branch']} path={row['path']} "
                f"derived={owner}/{kind}/{disposition} "
                f"accepted={row['acceptedOwner']}/{row['acceptedOwnerKind']}/{row['acceptedDisposition']}"
            )

    log.check("lifecycle.independent_rederivation", not mismatch,
              "all path owners/dispositions re-derived" if not mismatch else "; ".join(mismatch[:8]))

    kind_counts = Counter(kind for _, _, kind, _ in derived)
    disposition_counts = Counter(disposition for _, _, _, disposition in derived)
    admitted_self = sum(1 for _, _, kind, disp in derived if kind == "self" and disp == "ADMIT")
    admitted_ancestor = sum(1 for _, _, kind, disp in derived if kind == "ancestor" and disp == "ADMIT")
    unique_admitted = {(row["npc"], owner) for row, owner, _, disp in derived if owner and disp == "ADMIT"}

    observed = {
        "self_path_owners": kind_counts["self"],
        "ancestor_path_owners": kind_counts["ancestor"],
        "admitted_self_paths": admitted_self,
        "admitted_ancestor_paths": admitted_ancestor,
        "task_owned_suppressed_paths": disposition_counts["SUPPRESS_TASK_OWNED"],
        "same_visit_suppressed_paths": disposition_counts["SUPPRESS_SAME_VISIT"],
        "reversible_suppressed_paths": disposition_counts["SUPPRESS_REVERSIBLE"],
        "unique_admitted_owners": len(unique_admitted),
    }
    for key, value in observed.items():
        log.check(f"lifecycle.{key}", value == expected[key],
                  f"observed={value} expected={expected[key]}")

    ancestor_by_owner: dict[tuple[str, str], set[str]] = defaultdict(set)
    for row, owner, kind, disposition in derived:
        if owner and kind == "ancestor":
            ancestor_by_owner[(row["npc"], owner)].add(disposition)

    expected_ancestors = {
        (item["npc"], item["owner"]): item["disposition"]
        for item in expected["ancestor_owners"]
    }
    actual_ancestors = {}
    ambiguous = []
    for key, dispositions in ancestor_by_owner.items():
        if len(dispositions) != 1:
            ambiguous.append(f"{key} -> {sorted(dispositions)}")
        else:
            actual_ancestors[key] = next(iter(dispositions))

    log.check("lifecycle.ancestor_owner_disposition_consistency", not ambiguous,
              "one disposition per ancestor owner" if not ambiguous else "; ".join(ambiguous))
    log.check("lifecycle.ancestor_owner_set", actual_ancestors == expected_ancestors,
              f"observed={sorted((a,b,c) for (a,b),c in actual_ancestors.items())} "
              f"expected={sorted((a,b,c) for (a,b),c in expected_ancestors.items())}")

    controls = {
        ("npc_cultist", "@snake_1с"): "ADMIT",
        ("npc_merchant", "@merchant_2b"): "SUPPRESS_TASK_OWNED",
        ("npc_merchant", "@merchant_2e_1e"): "ADMIT",
        ("npc_merchant", "@merchant_favore_done"): "SUPPRESS_TASK_OWNED",
    }
    for key, expected_disp in controls.items():
        log.check(f"lifecycle.control.{key[0]}.{key[1]}",
                  actual_ancestors.get(key) == expected_disp,
                  f"observed={actual_ancestors.get(key)} expected={expected_disp}")

    diary = [
        (row, disp) for row, _, _, disp in derived
        if row["npc"] == "npc_astrologer" and row["branch"] in {"astrologer_diary_9a", "astrologer_diary_9b"}
    ]
    log.check("lifecycle.control.astrologer_diary_same_visit",
              len(diary) == 2 and all(disp == "SUPPRESS_SAME_VISIT" for _, disp in diary),
              f"rows={[(r['branch'], d) for r,d in diary]}")

    return {
        "records": len(rows),
        "derived_summary": observed,
        "ancestor_owners": [
            {"npc": npc, "owner": owner, "disposition": disp}
            for (npc, owner), disp in sorted(actual_ancestors.items())
        ],
    }


def parse_task_fixture(path: Path) -> dict:
    sections: dict[str, list[list[str]]] = defaultdict(list)
    section = None
    header = None
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            section = line[1:-1]
            header = None
            continue
        if section is None:
            raise ValueError(f"data outside section: {raw}")
        cols = raw.split("\t")
        if header is None:
            header = cols
        else:
            sections[section].append(dict(zip(header, cols)))
    return sections


def validate_task_census(sections: dict, baseline: dict, log: CheckLog) -> dict:
    expected = baseline["task_census"]
    npc_rows = sections.get("npc", [])
    candidate_rows = sections.get("candidate", [])
    mapped_controls = sections.get("selected-mapped-controls", [])

    observed_complete = sum(int(x["ownerComplete"]) for x in npc_rows)
    observed_selectable = sum(int(x["mappedSelectable"]) for x in npc_rows)
    observed_unresolved = sum(int(x["candidateNonSelectable"]) for x in npc_rows)

    log.check("tasks.npc_count", len(npc_rows) == 6, f"observed={len(npc_rows)} expected=6")
    log.check("tasks.owner_complete_nodes", observed_complete == expected["owner_complete_nodes"],
              f"observed={observed_complete} expected={expected['owner_complete_nodes']}")
    log.check("tasks.probe_selectable", observed_selectable == expected["probe_0_1_1_selectable"],
              f"observed={observed_selectable} expected={expected['probe_0_1_1_selectable']}")
    log.check("tasks.probe_unresolved", observed_unresolved == expected["probe_0_1_1_unresolved"],
              f"observed={observed_unresolved} expected={expected['probe_0_1_1_unresolved']}")

    expected_per_npc = {
        x["npc"]: (x["complete"], x["selectable"], x["unresolved"])
        for x in expected["per_npc"]
    }
    actual_per_npc = {
        x["npc"]: (int(x["ownerComplete"]), int(x["mappedSelectable"]), int(x["candidateNonSelectable"]))
        for x in npc_rows
    }
    log.check("tasks.per_npc_census", actual_per_npc == expected_per_npc,
              f"observed={actual_per_npc} expected={expected_per_npc}")

    expected_candidates = {(x["npc"], x["task"]) for x in expected["unresolved_resolution"]}
    actual_candidates = {(x["npc"], x["task"]) for x in candidate_rows}
    log.check("tasks.unresolved_candidate_set", actual_candidates == expected_candidates,
              f"observed={sorted(actual_candidates)} expected={sorted(expected_candidates)}")

    resolution = expected["unresolved_resolution"]
    recovered = sum(1 for x in resolution if x["kind"] == "selectable_event_hop")
    event_only = sum(1 for x in resolution if x["kind"] == "event_only")
    final_selectable = observed_selectable + recovered

    log.check("tasks.final_selectable", final_selectable == expected["final_selectable_task_completions"],
              f"observed={final_selectable} expected={expected['final_selectable_task_completions']}")
    log.check("tasks.final_event_only", event_only == expected["final_event_only_task_stages"],
              f"observed={event_only} expected={expected['final_event_only_task_stages']}")
    log.check("tasks.final_partition",
              final_selectable + event_only == observed_complete,
              f"selectable={final_selectable} eventOnly={event_only} total={observed_complete}")

    expected_controls = {
        ("npc_astrologer", "dlc_souls_s29_1", "@souls_s_s30_ask"),
        ("npc_cultist", "dlc_souls_s29_3", "@souls_s_s33_ask"),
        ("npc_actress", "dlc_souls_s29_2", "@souls_s_s31_ask"),
        ("npc_bishop", "bishop_rcitezen", "@bishop_get_citezen"),
    }
    actual_controls = {(x["npc"], x["task"], x["answer"]) for x in mapped_controls}
    log.check("tasks.selected_mapped_controls", actual_controls == expected_controls,
              f"observed={sorted(actual_controls)} expected={sorted(expected_controls)}")

    return {
        "owner_complete_nodes": observed_complete,
        "probe_selectable": observed_selectable,
        "probe_unresolved": observed_unresolved,
        "final_selectable": final_selectable,
        "final_event_only": event_only,
    }


def validate_task_routes(rows: list[dict[str, str]], baseline: dict, log: CheckLog) -> dict:
    expected = baseline["task_census"]
    selectable = [row for row in rows if row["kind"] == "SELECTABLE"]
    event_only = [row for row in rows if row["kind"] == "EVENT_OR_UNRESOLVED"]

    log.check("task_routes.total", len(rows) == expected["owner_complete_nodes"],
              f"observed={len(rows)} expected={expected['owner_complete_nodes']}")
    log.check("task_routes.selectable",
              len(selectable) == expected["final_selectable_task_completions"],
              f"observed={len(selectable)} expected={expected['final_selectable_task_completions']}")
    log.check("task_routes.event_only",
              len(event_only) == expected["final_event_only_task_stages"],
              f"observed={len(event_only)} expected={expected['final_event_only_task_stages']}")

    actual_event_only = {(row["npc"], row["task"]) for row in event_only}
    expected_event_only = {(row["npc"], row["task"]) for row in expected["event_only"]}
    log.check("task_routes.event_only_exact_set", actual_event_only == expected_event_only,
              f"observed={sorted(actual_event_only)} expected={sorted(expected_event_only)}")

    selectable_set = {(row["npc"], row["task"], row["answer"]) for row in selectable}
    derived_expected = {
        (row["npc"], row["task"], row["answer"])
        for row in baseline["verified_completion_supplement"]["derived_owner_routes"]
    }
    log.check("task_routes.derived_owner_routes", derived_expected.issubset(selectable_set),
              f"missing={sorted(derived_expected - selectable_set)}")

    return {
        "records": len(rows),
        "selectable": len(selectable),
        "event_only": len(event_only),
        "derived_owner_routes": sorted(derived_expected),
    }


def validate_multiple_answerdata(rows: list[dict[str, str]], baseline: dict, log: CheckLog) -> dict:
    expected = baseline["multiple_answerdata"]
    observed_set = {
        (row["npc"], int(row["multi"]), int(row["index"]), row["answer"], row["producer"], row["requirements"])
        for row in rows
    }
    expected_set = {
        ("npc_inquisitor", 736, 9, "@souls_s_s22_ask", "2118", "Item:ash_on_shawl=1,Item:sin_shard=1"),
        ("npc_inquisitor", 843, 15, "@souls_s_s22_ask", "2118", "Item:ash_on_shawl=1,Item:sin_shard=1"),
        ("npc_cultist", 106, 29, "@souls_s_s33_ask", "2560", "Item:note_with_rumors=1,Item:sin_shard=1"),
        ("npc_merchant", 38, 20, "@souls_s_s24_ask", "1701", "Item:sauce_for_meal=1,Item:sin_shard=1"),
        ("npc_bishop", 75, 8, "@souls_s_s15_ask", "2097", "Item:ode_for_bishop=1,Item:sin_shard=1"),
        ("npc_bishop", 679, 5, "@souls_s_s15_ask", "2097", "Item:ode_for_bishop=1,Item:sin_shard=1"),
        ("npc_bishop", 1030, 9, "@souls_s_s15_ask", "2097", "Item:ode_for_bishop=1,Item:sin_shard=1"),
    }
    log.check("multiple_answerdata.menu_use_count", len(rows) == expected["menu_uses"],
              f"observed={len(rows)} expected={expected['menu_uses']}")
    log.check("multiple_answerdata.exact_menu_use_set", observed_set == expected_set,
              f"observed={sorted(observed_set)} expected={sorted(expected_set)}")

    per_npc = Counter(row["npc"] for row in rows)
    expected_per_npc = {item["npc"]: item["uses"] for item in expected["per_npc"]}
    observed_per_npc = {npc: per_npc.get(npc, 0) for npc in expected_per_npc}
    log.check("multiple_answerdata.per_npc", observed_per_npc == expected_per_npc,
              f"observed={observed_per_npc} expected={expected_per_npc}")

    task_routes = {
        ("npc_inquisitor", "dlc_souls_s21_2", "@souls_s_s22_ask"),
        ("npc_cultist", "dlc_souls_s29_3", "@souls_s_s33_ask"),
        ("npc_merchant", "dlc_souls_s23_2", "@souls_s_s24_ask"),
        ("npc_bishop", "dlc_souls_s12_1", "@souls_s_s15_ask"),
    }
    expected_task_routes = {(x["npc"], x["task"], x["answer"]) for x in expected["task_routes"]}
    log.check("multiple_answerdata.task_route_set", task_routes == expected_task_routes,
              f"observed={sorted(task_routes)} expected={sorted(expected_task_routes)}")
    log.check("multiple_answerdata.native_semantics",
              expected["native_semantics"] == "AND across every child d_lock and d_price via WorldGameObject.IsEnough",
              expected["native_semantics"])

    return {
        "menu_uses": len(rows),
        "per_npc": observed_per_npc,
        "task_routes": sorted(task_routes),
    }


def extract_int_constant(text: str, name: str) -> int | None:
    match = re.search(rf"\b{name}\s*=\s*(\d+)\s*;", text)
    return int(match.group(1)) if match else None


def validate_production_source(baseline: dict, log: CheckLog) -> dict:
    manifest_path = ROOT / "src" / "PersistentRuleManifest.cs"
    rules_path = ROOT / "src" / "WeekdayInteractionRuleCache.cs"
    navigation_path = ROOT / "src" / "NavigationReachabilityCache.cs"
    lifecycle_path = ROOT / "src" / "UnifiedDialogueLifecycleCompiler.cs"
    completion_path = ROOT / "src" / "VerifiedCompletionReminderRules.cs"
    plugin_path = ROOT / "src" / "CalendarQuestsPinsPlugin.cs"

    manifest = manifest_path.read_text(encoding="utf-8")
    rules = rules_path.read_text(encoding="utf-8")
    navigation = navigation_path.read_text(encoding="utf-8")
    lifecycle = lifecycle_path.read_text(encoding="utf-8")
    completion = completion_path.read_text(encoding="utf-8")
    plugin = plugin_path.read_text(encoding="utf-8")

    expected_manifest = baseline["manifest"]
    constant_expectations = {
        "SchemaVersion": expected_manifest["schema"],
        "ExpectedOwnerSupported": expected_manifest["owner_supported"],
        "ExpectedOwnerUnsupported": expected_manifest["owner_unsupported"],
        "ExpectedCrossTasks": expected_manifest["cross_tasks"],
        "ExpectedCrossSupported": expected_manifest["cross_supported"],
        "ExpectedCrossUnsupported": expected_manifest["cross_unsupported"],
        "ExpectedAtTopics": expected_manifest["base_at_topics"],
        "ExpectedAtTopicSupported": expected_manifest["base_at_supported"],
        "ExpectedAtTopicUnsupported": expected_manifest["base_at_unsupported"],
    }
    observed_constants = {}
    for name, expected in constant_expectations.items():
        observed = extract_int_constant(manifest, name)
        observed_constants[name] = observed
        log.check(f"source.manifest.{name}", observed == expected,
                  f"observed={observed} expected={expected}")

    lifecycle_expected = {
        "ExpectedGraphCount": 6,
        "ExpectedNonAtUnique": baseline["lifecycle"]["non_at_unique"],
        "ExpectedExactSelf": baseline["lifecycle"]["exact_self"],
        "ExpectedAncestorOwnerCandidates": baseline["lifecycle"]["ancestor_owner_candidates"],
        "ExpectedAncestorTaskExcluded": baseline["lifecycle"]["ancestor_task_excluded"],
        "ExpectedAncestorAdmittedTopics": baseline["lifecycle"]["ancestor_admitted_topics"],
    }
    observed_lifecycle_constants = {}
    for name, expected in lifecycle_expected.items():
        observed = extract_int_constant(lifecycle, name)
        observed_lifecycle_constants[name] = observed
        log.check(f"source.lifecycle.{name}", observed == expected,
                  f"observed={observed} expected={expected}")

    semantic_needles = [
        "if (branch.Blacklists.Contains(branch.Key.AnswerId)) continue;",
        "for (var a = path.Ancestors.Count - 1; a >= 0; a--)",
        "if (completionAnswerIds.Contains(ownerId))",
        "if (!navigation.HasInteractionRootPathWithoutAncestors",
        "if (removals.Contains(ownerId))",
    ]
    for needle in semantic_needles:
        log.check("source.lifecycle.guard." + str(abs(hash(needle))),
                  needle in lifecycle, f"required semantic guard missing: {needle}")

    promoted_pattern = re.compile(
        r'new\s+PromotedRoute\s*\{\s*NpcId\s*=\s*"([^"]+)"\s*,\s*'
        r'TaskId\s*=\s*"([^"]+)"\s*,\s*AnswerId\s*=\s*"([^"]+)"\s*\}'
    )
    promoted_actual = set(promoted_pattern.findall(completion))
    promoted_expected = {
        (x["npc"], x["task"], x["answer"])
        for x in baseline["verified_completion_supplement"]["promoted_routes"]
    }
    log.check("source.completion.promoted_routes", promoted_actual == promoted_expected,
              f"observed={sorted(promoted_actual)} expected={sorted(promoted_expected)}")

    event_pattern = re.compile(
        r'if\s*\(string\.Equals\(target\.NpcId,\s*"([^"]+)"[^)]*\)\s*&&\s*'
        r'string\.Equals\(taskId,\s*"([^"]+)"[^)]*\)\)\s*return\s+true\s*;',
        re.S,
    )
    event_actual = set(event_pattern.findall(completion))
    event_expected = {(x["npc"], x["task"]) for x in baseline["task_census"]["event_only"]}
    log.check("source.completion.event_only_exact_set", event_actual == event_expected,
              f"observed={sorted(event_actual)} expected={sorted(event_expected)}")

    answer_backed_special_tokens = [
        "PromotedRoute",
        "IsPromotedCompletionTopic",
        '"snake_trap"',
        '"snake_stone_ready"',
        'CreateSmartRes("GameRes", "_rel", 10f',
    ]
    leaked_specials = [token for token in answer_backed_special_tokens if token in completion or token in plugin]
    log.check("source.completion.no_answer_backed_specials", not leaked_specials,
              f"answer-backed completion specials must be generic owner-task rules; leaked={leaked_specials}")

    owner_task_topology_needles = [
        "BuildOwnerTaskIncomingFlow",
        "Flow_WaitForFlow",
        "IsIntegerPort(c.TargetPort)",
        '"_sourceOutputUID"',
        '"_UID"',
        "AddOwnerTaskFunctionLinks",
        "Flow_FireEvent",
        "CustomEvent",
        "eventName",
        "AddOwnerTaskEventLinks",
        "var anchorFlow = ownerLocal ? ownerTaskIncomingFlow : incomingFlow;",
    ]
    for needle in owner_task_topology_needles:
        log.check("source.owner_task_topology.guard." + str(abs(hash(needle))),
                  needle in rules, f"required bounded owner-task topology guard missing: {needle}")

    retired_snake_special = ("@snake_1с" in completion or "quest_fake_coins" in completion or
                             "@snake_1с" in plugin or "quest_fake_coins" in plugin)
    log.check("source.no_retired_snake_fake_coins_special", not retired_snake_special,
              "old 1.1.5 Snake fake-coins hard-code must not return")

    manifest_uses_lifecycle_validation = "UnifiedDialogueLifecycleCompiler.Validate" in manifest
    log.check("source.manifest.uses_lifecycle_validation", manifest_uses_lifecycle_validation,
              "manifest must reject lifecycle-universe drift")

    multiple_answer_needles = [
        "TryBuildVariantFromAnswerDataSource",
        "BuildMultipleAnswerVariant",
        "\"_sourceInputUID\"",
        "\"Flow_MultipleAnswer\"",
        "\"Flow_AnswersArray\"",
        "AdditionalRequirements",
    ]
    for needle in multiple_answer_needles:
        log.check("source.multiple_answer.guard." + str(abs(hash(needle))),
                  needle in rules, f"required MultipleAnswerData compiler guard missing: {needle}")

    log.check("source.multiple_answer.runtime_and_evaluation",
              "AreAdditionalRequirementsEnough" in navigation and
              "variant.AdditionalRequirements" in navigation,
              "compound requirements must be AND-evaluated through the normal live requirement path")

    forbidden_specifics = ["note_with_rumors", "ash_on_shawl", "sauce_for_meal", "ode_for_bishop"]
    combined_production = rules + navigation + completion + plugin
    for token in forbidden_specifics:
        log.check("source.multiple_answer.no_specific_item." + token,
                  token not in combined_production,
                  f"generic production compiler must not hard-code {token}")

    return {
        "manifest_constants": observed_constants,
        "lifecycle_constants": observed_lifecycle_constants,
        "promoted_routes": sorted(promoted_actual),
        "event_only": sorted(event_actual),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--lifecycle", type=Path, default=DEFAULT_LIFECYCLE)
    parser.add_argument("--tasks", type=Path, default=DEFAULT_TASKS)
    parser.add_argument("--task-routes", type=Path, default=DEFAULT_TASK_ROUTES)
    parser.add_argument("--multiple-answer", type=Path, default=DEFAULT_MULTIPLE_ANSWER)
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    args = parser.parse_args()

    baseline = load_json(args.baseline)
    log = CheckLog()

    lifecycle_rows = load_lifecycle(args.lifecycle)
    lifecycle_report = validate_lifecycle(lifecycle_rows, baseline, log)
    task_sections = parse_task_fixture(args.tasks)
    task_report = validate_task_census(task_sections, baseline, log)
    task_route_rows = load_tsv(args.task_routes)
    task_route_report = validate_task_routes(task_route_rows, baseline, log)
    multiple_answer_rows = load_tsv(args.multiple_answer)
    multiple_answer_report = validate_multiple_answerdata(multiple_answer_rows, baseline, log)
    source_report = validate_production_source(baseline, log)

    # This is an explicit coverage boundary, not a hidden pass condition.
    log.warn(
        "Navigation has accepted runtime totals 210 answers / 270 paths / 151 predicates / 0 unsupported, "
        "but no complete path fixture is yet stored. Lifecycle fixture coverage exercises many of those paths "
        "but is not a complete navigation oracle."
    )

    report = {
        "validator_format": 1,
        "baseline_version": baseline["baseline_version"],
        "game": baseline["game"],
        "status": "PASS" if not log.failures else "FAIL",
        "checks_total": len(log.checks),
        "checks_failed": len(log.failures),
        "failures": log.failures,
        "warnings": log.warnings,
        "coverage": {
            "dialogue_lifecycle": "path-level exhaustive for accepted census",
            "owner_task_completion": "route-level exhaustive: 72 completion routes = 70 selectable + 2 event-only",
            "navigation": "accepted totals plus lifecycle-path coverage; complete standalone path fixture pending",
            "event_only": "exact accepted set",
            "production_contract": "static bounded contract checks",
        },
        "lifecycle": lifecycle_report,
        "tasks": task_report,
        "task_routes": task_route_report,
        "multiple_answerdata": multiple_answer_report,
        "production_source": source_report,
        "checks": log.checks,
    }

    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    print(f"Day Wheel interaction validator: {report['status']}")
    print(f"Checks: {report['checks_total']} total, {report['checks_failed']} failed")
    print(
        "Lifecycle: "
        f"{lifecycle_report['records']} path records, "
        f"{lifecycle_report['derived_summary']['unique_admitted_owners']} unique admitted owners"
    )
    print(
        "Tasks: "
        f"{task_report['owner_complete_nodes']} completion nodes -> "
        f"{task_report['final_selectable']} selectable + {task_report['final_event_only']} event-only"
    )
    print(
        "Task routes: "
        f"{task_route_report['records']} exact routes -> "
        f"{task_route_report['selectable']} selectable + {task_route_report['event_only']} event-only"
    )
    print(
        "MultipleAnswerData: "
        f"{multiple_answer_report['menu_uses']} verified menu uses across "
        f"{sum(1 for x in multiple_answer_report['per_npc'].values() if x > 0)} weekday NPCs"
    )
    for warning in log.warnings:
        print("COVERAGE NOTE:", warning)
    for failure in log.failures:
        print("FAIL:", failure)
    print(f"Report: {args.report.relative_to(ROOT)}")
    return 1 if log.failures else 0


if __name__ == "__main__":
    sys.exit(main())
