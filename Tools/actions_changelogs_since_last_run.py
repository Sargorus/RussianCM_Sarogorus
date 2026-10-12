#!/usr/bin/env python3
# CMU14 Begin: downstream changelog assembly and Russian Discord delivery.
"""Translate integrated changelogs and publish using a durable delivery journal.

--dry-run reads journal state and writes a preview, without Discord or journal writes.
Upstream entries are read from this checkout, never from an upstream's current HEAD.
"""

import argparse
import copy
import datetime as dt
import hashlib
import json
import os
import re
import sys
import time
from pathlib import Path
from urllib.parse import urlparse

import requests

sys.path.insert(0, str(Path(__file__).resolve().parent / "_CMU14" / "Changelog"))

from changelog_discord_state import FileStateStore, GitHubStateStore, empty_state
from changelog_translation import RussianTranslator, POLICY_FILE
from changelog_translation_cache import load_translation_cache, save_translation_cache
from update_changelog import load_yaml, entry_key
from update_changelog_parts import parse_time

DEFAULT_FILES = [f"Resources/Changelog/{name}.yml" for name in
                 ("CMU", "RMC14", "Changelog", "Maps", "Admin", "Rules")]
SECTION_NAMES = {"CMU": "CMU", "RMC14": "RMC14", "Changelog": "SS14",
                 "Maps": "Карты", "Admin": "Администрирование", "Rules": "Правила"}
TYPES_TO_EMOJI = {"Fix": "🔧", "Add": "✨", "Remove": "🔥", "Tweak": "🎚️",
                  "Code": "🛠️", "Map": "📍", "Admin": "🛡️"}
DISCORD_SPLIT_LIMIT = 2000


class DeliveryRejected(RuntimeError):
    """Discord explicitly rejected a message, so retrying is safe."""


def entry_signature(entry):
    return entry_key(entry)


def diff_changelog(old, current):
    previous = {entry_signature(e) for e in (old or {}).get("Entries", [])}
    seen = set(previous)
    diff = []
    for entry in (current or {}).get("Entries", []):
        key = entry_signature(entry)
        if key not in seen:
            diff.append(entry)
            seen.add(key)
    return diff


def delivery_key(source, entry):
    return hashlib.sha256(json.dumps([source, entry_signature(entry)], ensure_ascii=False).encode()).hexdigest()


def entry_date(entry):
    if not entry.get("time"):
        # Legacy hand-written records without dates are historical at bootstrap,
        # but a newly arriving undated record is still delivered after initialization.
        return dt.datetime.min.replace(tzinfo=dt.timezone.utc)
    return parse_time(str(entry["time"]))


def load_entries(files):
    entries = {}
    for filename in files:
        source = Path(filename).stem
        for raw in load_yaml(filename).get("Entries", []):
            entry = copy.deepcopy(raw)
            if not entry.get("changes"):
                continue
            entry_date(entry)
            key = delivery_key(source, entry)
            if key in entries:
                for change in entry["changes"]:
                    if change not in entries[key]["changes"]:
                        entries[key]["changes"].append(change)
                continue
            entry["source"] = source
            entry["key"] = key
            entries[key] = entry
    return sorted(entries.values(), key=lambda e: (entry_date(e), e["source"], e["key"]))


def initial_state(entries, start_date=None, now=None):
    state = empty_state()
    now = now or dt.datetime.now(dt.timezone.utc)
    cutoff = parse_time(start_date) if start_date else now - dt.timedelta(days=30)
    state["bootstrap_since"] = cutoff.isoformat()
    state["baseline"] = [e["key"] for e in entries if entry_date(e) < cutoff]
    print(f"First run: recover entries since {cutoff.isoformat()}; older entries form a baseline")
    return state


def select_entries(entries, state, start_date=None):
    baseline = set(state["baseline"])
    cutoff = parse_time(start_date) if start_date else None
    # New upstream arrivals can have old timestamps. Do not use time as a delivery cursor.
    return [e for e in entries if e["key"] not in state["delivered"]
            and (e["key"] not in baseline or
                 (cutoff is not None and entry_date(e) >= cutoff))]


def discord_length(text):
    return len(text.encode("utf-16-le")) // 2


def split_text(text, limit):
    remaining = text
    while remaining:
        size = 0
        end = 0
        for char in remaining:
            added = 2 if ord(char) > 0xFFFF else 1
            if size + added > limit:
                break
            size += added
            end += 1
        if not end:
            raise ValueError("No room for changelog text")
        if end < len(remaining):
            boundary = remaining.rfind(" ", 0, end)
            if boundary > end // 2:
                end = boundary + 1
        yield remaining[:end]
        remaining = remaining[end:]


def escape_author(value):
    return re.sub(r"([\\*_~`|<>])", r"\\\1", str(value)).replace("\n", " ")[:150]


def entry_chunks(entry):
    section = SECTION_NAMES.get(entry["source"], entry["source"])
    date = entry_date(entry)
    date_label = f" · {date:%d.%m.%Y}" if date.year > 1 else ""
    heading = f"**{section} · {escape_author(entry['author'])}**{date_label}\n"
    url = str(entry.get("url") or "").strip()
    footer = ""
    if url:
        parsed = urlparse(url)
        if parsed.scheme != "https" or parsed.netloc != "github.com" or not re.fullmatch(r"/[^/]+/[^/]+/pull/\d+/?", parsed.path):
            raise ValueError("Invalid changelog PR link")
        footer = f"[Изменение #{parsed.path.rstrip('/').split('/')[-1]}]({url})\n"
    limit = DISCORD_SPLIT_LIMIT - discord_length(heading + footer)
    if limit < 100:
        raise ValueError("Changelog header is too long")
    lines = []
    for change in entry["changes"]:
        emoji = TYPES_TO_EMOJI.get(change["type"], "❓")
        if "Intent: Experimental" in entry.get("labels", []):
            emoji += "🧪"
        prefix = emoji + " "
        for fragment in split_text(change["message"], limit - discord_length(prefix + "\n")):
            lines.append(prefix + fragment + "\n")
    chunks = []
    current = ""
    for line in lines:
        if current and discord_length(current + line) > limit:
            chunks.append(heading + current + footer)
            current = ""
        current += line
    if current:
        chunks.append(heading + current + footer)
    return chunks


def prepare_delivery(entries, translator):
    messages = [c["message"] for e in entries for c in e["changes"]]
    translated = iter(translator.translate(messages))
    chunks = []
    for raw in entries:
        entry = copy.deepcopy(raw)
        for change in entry["changes"]:
            change["message"] = next(translated)
        chunks.extend(entry_chunks(entry))
    return {"keys": [e["key"] for e in entries], "chunks": chunks, "next": 0, "receipts": []}


def get_discord_body(content):
    return {"content": content, "allowed_mentions": {"parse": []}, "flags": 1 << 2}


def send_discord_webhook(content, webhook_url=None):
    webhook_url = webhook_url or os.environ.get("DISCORD_WEBHOOK_URL")
    if not webhook_url:
        raise RuntimeError("CHANGELOG_DISCORD_WEBHOOK is missing")
    for attempt in range(8):
        try:
            response = requests.post(webhook_url, params={"wait": "true"},
                                     json=get_discord_body(content), timeout=30)
        except requests.RequestException:
            raise RuntimeError("Discord connection failed; delivery is paused for channel verification.") from None
        if response.status_code == 429:
            delay = float(response.json().get("retry_after", 5))
            if not 0 <= delay <= 60:
                raise DeliveryRejected("Discord rate limit exceeds retry budget")
            time.sleep(delay)
            continue
        if response.status_code != 200:
            if response.status_code < 500:
                raise DeliveryRejected(f"Discord returned HTTP {response.status_code}; delivery stopped")
            raise RuntimeError(f"Discord returned HTTP {response.status_code}; delivery is paused for verification")
        receipt = response.json().get("id")
        if not receipt:
            raise RuntimeError("Discord did not confirm a message ID")
        return receipt
    raise DeliveryRejected("Discord rate limit retries exhausted")


def publish_pending(state, store, sender=send_discord_webhook):
    pending = state["pending"]
    if not pending:
        return
    if pending.get("in_flight") is not None:
        raise RuntimeError("An interrupted Discord request needs verification; use --resolve-ambiguous after checking the channel")
    while pending["next"] < len(pending["chunks"]):
        index = pending["next"]
        pending["in_flight"] = index
        store.save(state)
        try:
            receipt = sender(pending["chunks"][index])
        except DeliveryRejected:
            pending["in_flight"] = None
            store.save(state)
            raise
        pending["receipts"].append(receipt)
        pending["next"] += 1
        pending["in_flight"] = None
        store.save(state)
        print(f"Discord confirmed part {pending['next']}/{len(pending['chunks'])}")
    for key in pending["keys"]:
        state["delivered"][key] = dt.datetime.now(dt.timezone.utc).isoformat()
    state["pending"] = None
    store.save(state)


def resolve_ambiguous(state, resolution, store):
    pending = state["pending"]
    if not pending or pending.get("in_flight") is None:
        raise RuntimeError("No ambiguous Discord delivery to resolve")
    if resolution == "delivered":
        pending["receipts"].append("operator-confirmed")
        pending["next"] += 1
    pending["in_flight"] = None
    store.save(state)


def write_preview(path, chunks):
    text = "# Предпросмотр чейнджлога Discord\n\nНичего не отправлено.\n\n"
    for i, content in enumerate(chunks, 1):
        text += f"## Сообщение {i} ({discord_length(content)}/2000)\n\n{content}\n"
    if not chunks:
        text += "Нет новых записей для отправки.\n"
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(text, encoding="utf-8", newline="\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--files", nargs="+", default=DEFAULT_FILES)
    parser.add_argument("--start-date", default=os.environ.get("CHANGELOG_START_DATE") or None)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--output", default="discord-preview.md")
    parser.add_argument("--translation-cache", default=os.environ.get("CHANGELOG_TRANSLATION_CACHE"),
                        help="Reusable validated translations; preview may write this cache, never delivery state")
    parser.add_argument("--state-file", help="Local journal for tests/preview; production uses GitHub")
    parser.add_argument("--translation-policy", default=str(POLICY_FILE))
    parser.add_argument("--resolve-ambiguous", choices=["retry", "delivered"],
                        help="Operator decision after checking the channel for an interrupted request")
    args = parser.parse_args()
    if args.start_date:
        parse_time(args.start_date)
    if args.dry_run and args.resolve_ambiguous:
        raise RuntimeError("Preview cannot resolve or change delivery state")
    if not args.dry_run and not os.environ.get("DISCORD_WEBHOOK_URL"):
        raise RuntimeError("CHANGELOG_DISCORD_WEBHOOK is missing; nothing will be marked delivered")
    if args.state_file:
        store = FileStateStore(args.state_file)
    else:
        store = GitHubStateStore(os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_TOKEN"])
    entries = load_entries(args.files)
    state = store.load()
    if state is None:
        state = initial_state(entries, args.start_date)
        if not args.dry_run:
            store.save(state)
    if state["pending"]:
        if args.dry_run:
            write_preview(args.output, state["pending"]["chunks"][state["pending"]["next"]:])
            print("Previewing remaining parts of an interrupted delivery")
            return
        if args.resolve_ambiguous:
            resolve_ambiguous(state, args.resolve_ambiguous, store)
        publish_pending(state, store)
    elif args.resolve_ambiguous:
        raise RuntimeError("No ambiguous Discord delivery to resolve")
    selected = select_entries(entries, state, args.start_date)
    print(f"Integrated={len(entries)} Delivered={len(state['delivered'])} Pending={len(selected)}")
    if not selected:
        if args.dry_run:
            write_preview(args.output, [])
        return
    if args.translation_cache:
        reusable = load_translation_cache(args.translation_cache)
        reusable.update(state["translations"])
        state["translations"] = reusable

    def checkpoint_translation():
        if args.translation_cache:
            save_translation_cache(args.translation_cache, state["translations"])
        if not args.dry_run:
            store.save(state)

    translator = RussianTranslator(state["translations"], args.translation_policy, checkpoint=checkpoint_translation)
    try:
        pending = prepare_delivery(selected, translator)
    except Exception:
        if not args.dry_run:
            store.save(state)
        raise
    checkpoint_translation()
    if args.dry_run:
        write_preview(args.output, pending["chunks"])
        print(f"Preview written to {args.output}; no Discord or journal writes")
        return
    state["pending"] = pending
    store.save(state)
    publish_pending(state, store)


if __name__ == "__main__":
    try:
        main()
    except (requests.RequestException, KeyError):
        print("Changelog failed: journal connection or configuration error", file=sys.stderr)
        sys.exit(1)
    except Exception as error:
        print(f"Changelog failed: {error}", file=sys.stderr)
        sys.exit(1)
# CMU14 End
