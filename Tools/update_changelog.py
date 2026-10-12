#!/usr/bin/env python3
# CMU14 Begin: downstream changelog assembly and Russian Discord delivery.
"""
Assemble changelog .yml parts into a changelog file.

Entry IDs are append-only and never renumbered. This is required by the client,
which persists the last-read ID between sessions. Old entries may be pruned, but
surviving IDs remain stable and new entries always use max(existing id) + 1.

usage: update_changelog.py <changelog-file> <parts-dir> --category "Main"
"""

import argparse
import datetime
import hashlib
import json
import re
from pathlib import Path
from typing import Any

import yaml

MAX_ENTRIES = 1500
CATEGORY_MAIN = "Main"
CHANGELOG_CATEGORIES = {"CMU": "CMU", "RMC14": "RMC14", "Main": "Changelog",
                        "Maps": "Maps", "Admin": "Admin", "Rules": "Rules"}


# Prevent PyYAML from turning ISO-8601 strings into datetime instances and then
# serializing them in a different format on every changelog update.
class NoDatesSafeLoader(yaml.SafeLoader):
    @classmethod
    def remove_implicit_resolver(cls, tag_to_remove):
        if "yaml_implicit_resolvers" not in cls.__dict__:
            cls.yaml_implicit_resolvers = cls.yaml_implicit_resolvers.copy()

        for first_letter, mappings in cls.yaml_implicit_resolvers.items():
            cls.yaml_implicit_resolvers[first_letter] = [
                (tag, regexp) for tag, regexp in mappings if tag != tag_to_remove
            ]


NoDatesSafeLoader.remove_implicit_resolver("tag:yaml.org,2002:timestamp")


def entry_sort_key(entry: dict[str, Any]) -> tuple:
    timestamp = entry.get("time")
    parsed = datetime.datetime.min.replace(tzinfo=datetime.timezone.utc)
    if timestamp:
        timestamp = re.sub(r"^(\d{4})-(\d{1,2})-(\d{1,2})(?=T|$)",
                           lambda m: f"{m[1]}-{int(m[2]):02d}-{int(m[3]):02d}", str(timestamp))
        parsed = datetime.datetime.fromisoformat(timestamp.replace("Z", "+00:00"))
        if parsed.tzinfo is None:
            parsed = parsed.replace(tzinfo=datetime.timezone.utc)
    return (parsed, int(entry.get("id", 0)))


def load_yaml(path: str) -> dict[str, Any]:
    with open(path, "r", encoding="utf-8-sig") as f:
        return yaml.load(f, Loader=NoDatesSafeLoader) or {}


def entry_key(entry: dict[str, Any]) -> tuple:
    url = str(entry.get("url") or "").strip().rstrip("/")
    if url:
        return ("url", url)
    timestamp = entry.get("time")
    if timestamp:
        timestamp = entry_sort_key(entry)[0].astimezone(datetime.timezone.utc).isoformat()
    fingerprint = hashlib.sha256(json.dumps(entry.get("changes", []), sort_keys=True).encode()).hexdigest()
    return ("manual", entry.get("author"), timestamp, fingerprint)


def assemble(changelog_file: str, parts_dir: str, category: str, untagged_category=None) -> None:
    current_data = load_yaml(changelog_file)
    original = current_data.get("Entries", [])
    max_id = max((int(e.get("id", 0)) for e in original), default=0)
    entries_list = []
    existing = {}
    used_ids = set()
    # Merge duplicate URLs without discarding changes or renumbering surviving IDs.
    for entry in original:
        key = entry_key(entry)
        if key in existing:
            # Retain the highest already-issued ID for this PR. Otherwise removing
            # a high-ID duplicate could make future entries invisible to clients.
            existing[key]["id"] = max(existing[key]["id"], entry["id"])
            used_ids.add(existing[key]["id"])
            for change in entry["changes"]:
                if change not in existing[key]["changes"]:
                    existing[key]["changes"].append(change)
            continue
        if entry["id"] in used_ids:
            max_id += 1
            entry["id"] = max_id
        used_ids.add(entry["id"])
        existing[key] = entry
        entries_list.append(entry)

    # A duplicate URL can itself carry an ID already used by another PR.
    # Check once more after merging duplicate records.
    used_ids.clear()
    for entry in entries_list:
        if entry["id"] in used_ids:
            max_id += 1
            entry["id"] = max_id
        used_ids.add(entry["id"])

    consumed = []
    for partpath in sorted(Path(parts_dir).glob("*.yml")):
        part = load_yaml(str(partpath))
        if part.get("category", untagged_category or category) != category:
            continue
        part.setdefault("time", datetime.datetime.now(datetime.timezone.utc).isoformat())
        changes = part["changes"]
        if not isinstance(changes, list):
            changes = [changes]
        # Validate all parts before replacing the changelog or deleting inputs.
        for change in changes:
            if not isinstance(change, dict) or not isinstance(change.get("message"), str):
                raise ValueError(f"Invalid changelog change in {partpath}")
            if change.get("type") not in {"Add", "Remove", "Tweak", "Fix", "Map", "Code", "Admin"}:
                raise ValueError(f"Invalid changelog type in {partpath}")
        key = entry_key(part)
        if changes and key not in existing:
            max_id += 1
            entry = {"author": part["author"], "time": part["time"],
                     "changes": changes, "id": max_id, "url": part.get("url")}
            if part.get("labels"):
                entry["labels"] = part["labels"]
            entries_list.append(entry)
            existing[key] = entry
        consumed.append(partpath)

    entries_list.sort(key=entry_sort_key)
    retained = entries_list[-MAX_ENTRIES:]
    # An old upstream record can have the largest ID after a collision repair.
    # Keep that high-water entry so pruning cannot cause future ID reuse.
    if retained and entries_list and max(e["id"] for e in retained) < max_id:
        high_water = max(entries_list, key=lambda e: e["id"])
        retained = ([high_water] + retained[1:]) if high_water not in retained else retained
        retained.sort(key=entry_sort_key)
    current_data["Entries"] = retained
    output = yaml.safe_dump(current_data, allow_unicode=True, sort_keys=False)
    target = Path(changelog_file)
    if target.read_text(encoding="utf-8-sig") != output:
        temporary = target.with_suffix(target.suffix + ".tmp")
        temporary.write_text(output, encoding="utf-8", newline="\n")
        temporary.replace(target)
    for partpath in consumed:
        partpath.unlink()
    print(f"Have {len(current_data['Entries'])} entries; consumed {len(consumed)} parts")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("changelog_file")
    parser.add_argument("parts_dir")
    parser.add_argument("--category", default=CATEGORY_MAIN)
    parser.add_argument("--all", action="store_true", help="Assemble all supported sections next to the target file")
    args = parser.parse_args()
    if args.all:
        if args.category not in CHANGELOG_CATEGORIES:
            raise ValueError(f"Unknown changelog category: {args.category}")
        for category, filename in CHANGELOG_CATEGORIES.items():
            target = Path(args.changelog_file).parent / f"{filename}.yml"
            assemble(str(target), args.parts_dir, category, untagged_category=args.category)
    else:
        assemble(args.changelog_file, args.parts_dir, args.category)


if __name__ == "__main__":
    main()
# CMU14 End
