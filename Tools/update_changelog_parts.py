#!/usr/bin/env python3
# CMU14 Begin: downstream changelog assembly and Russian Discord delivery.
"""
Extract changelog .yml parts from merged PRs via their :cl: blocks.

The scan cursor is derived from changelog entries belonging to this repository,
not from GitHub Actions run history. A small overlap plus URL deduplication makes
re-runs, delayed search indexing, scheduled runs and reusable-workflow calls safe.

usage: update_changelog_parts.py <parts-dir> [--changelog-file FILE]
                                 [--category "Main"]
"""

import argparse
import datetime
import hashlib
import os
import re
from pathlib import Path

import requests
import yaml

CATEGORY_MAIN = "Main"
GITHUB_API_URL = "https://api.github.com"
FALLBACK_DATE = "2025-06-01T00:00:00Z"
INITIAL_LOOKBACK_DAYS = 30
REPOSITORY_OVERLAP_DAYS = 7

COMMENT_RE = re.compile(r"<!--.*?-->", re.DOTALL)
HEADER_RE = re.compile(
    r"^\s*(?::cl:|🆑)\s*([^\n\r]*)$", re.IGNORECASE | re.MULTILINE
)
ENTRY_RE = re.compile(
    r"^ *[*-]? *(add|remove|tweak|fix|map|code|admin): *([^\n\r]+)\r?$",
    re.IGNORECASE | re.MULTILINE,
)

TYPE_MAP = {
    "add": "Add",
    "remove": "Remove",
    "tweak": "Tweak",
    "fix": "Fix",
    "map": "Map",
    "code": "Code",
    "admin": "Admin",
}
DEFAULT_MESSAGES = {
    "Added fun!",
    "Removed fun!",
    "Changed fun!",
    "Fixed fun!",
    "Mapped fun!",
    "Admin related change!",
    "Code related change for contributors!",
}


def make_session(token: str) -> requests.Session:
    sess = requests.Session()
    sess.headers.update(
        {
            "Authorization": f"Bearer {token}",
            "X-GitHub-Api-Version": "2022-11-28",
            "Accept": "application/vnd.github+json",
        }
    )
    return sess


def parse_time(value: str) -> datetime.datetime:
    # Historical upstream files contain dates such as 2024-1-4; normalize only
    # that known representation, while still rejecting genuinely invalid dates.
    value = re.sub(r"^(\d{4})-(\d{1,2})-(\d{1,2})(?=T|$)",
                   lambda m: f"{m[1]}-{int(m[2]):02d}-{int(m[3]):02d}", str(value))
    parsed = datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=datetime.timezone.utc)
    return parsed.astimezone(datetime.timezone.utc)


def format_time(value: datetime.datetime) -> str:
    return value.astimezone(datetime.timezone.utc).isoformat().replace("+00:00", "Z")


def get_scan_start(entries: list[dict], repo: str, explicit_start: str | None) -> str:
    if explicit_start:
        # Validate early so a typo does not silently create an invalid GitHub query.
        parse_time(explicit_start)
        return explicit_start

    repo_prefix = f"https://github.com/{repo}/pull/"
    repo_times = [
        parse_time(entry["time"])
        for entry in entries
        if str(entry.get("url", "")).startswith(repo_prefix) and entry.get("time")
    ]
    if repo_times:
        # Search with overlap to tolerate delayed GitHub search indexing and then
        # deduplicate by PR URL. This is also safe after long workflow downtime.
        latest = max(repo_times) - datetime.timedelta(days=REPOSITORY_OVERLAP_DAYS)
        return format_time(latest)

    all_times = [parse_time(entry["time"]) for entry in entries if entry.get("time")]
    if all_times:
        # Migration path for a changelog that previously only contained upstream
        # entries: import a bounded recent window of local PRs instead of all history.
        latest = max(all_times) - datetime.timedelta(days=INITIAL_LOOKBACK_DAYS)
        return format_time(latest)

    return FALLBACK_DATE


def get_merged_prs(sess: requests.Session, repo: str, since: str, base: str | None = None) -> list[dict]:
    prs = []
    page = 1
    while True:
        q = f"repo:{repo} is:pr is:merged merged:>={since}"
        if base:
            q += f" base:{base}"
        resp = sess.get(
            f"{GITHUB_API_URL}/search/issues",
            params={
                "q": q,
                "sort": "created",
                "order": "asc",
                "per_page": 100,
                "page": page,
            },
            timeout=30,
        )
        resp.raise_for_status()
        items = resp.json()["items"]
        prs.extend(items)
        if len(items) < 100:
            break
        page += 1
        if page > 10:
            raise RuntimeError("GitHub search cap of 1000 merged PRs was reached")
    return prs


def fetch_pr(sess: requests.Session, repo: str, number: int) -> dict:
    resp = sess.get(f"{GITHUB_API_URL}/repos/{repo}/pulls/{number}", timeout=30)
    resp.raise_for_status()
    return resp.json()


def parse_cl_block(body: str, pr_author: str) -> tuple[str, list[dict]] | None:
    body = COMMENT_RE.sub("", body)
    match = HEADER_RE.search(body)
    if not match:
        return None

    author = (match.group(1) or "").strip() or pr_author
    changes = [
        {"type": TYPE_MAP[m.group(1).lower()], "message": m.group(2).strip()}
        for m in ENTRY_RE.finditer(body[match.end() :])
        if m.group(2).strip() not in DEFAULT_MESSAGES
    ]
    return (author, changes) if changes else None


def load_changelog(changelog_file: str) -> dict:
    path = Path(changelog_file)
    if not path.exists():
        return {}
    with path.open("r", encoding="utf-8-sig") as f:
        return yaml.safe_load(f) or {}


def get_seen_urls(entries: list[dict], parts_dir: str) -> set[str]:
    seen = {str(entry["url"]) for entry in entries if entry.get("url")}

    for partpath in Path(parts_dir).glob("*.yml"):
        try:
            with partpath.open("r", encoding="utf-8-sig") as f:
                part = yaml.safe_load(f) or {}
        except (OSError, yaml.YAMLError):
            continue
        if part.get("url"):
            seen.add(str(part["url"]))

    return seen


def write_part(
    parts_dir: str,
    pr_number: int,
    author: str,
    changes: list,
    time: str,
    url: str,
    category: str,
    labels: list[str],
) -> bool:
    part = {
        "author": author,
        "changes": changes,
        "time": time,
        "url": url,
        "category": category,
    }
    if labels:
        part["labels"] = labels

    # Local and upstream repositories can use the same PR number in the shared Parts directory.
    namespace = hashlib.sha256(url.rstrip("/").encode()).hexdigest()[:12]
    path = os.path.join(parts_dir, f"pr-{pr_number}-{namespace}.yml")
    if os.path.exists(path):
        print(f"Part for PR #{pr_number} already exists, skipping.")
        return False

    with open(path, "w", encoding="utf-8") as f:
        yaml.safe_dump(part, f, allow_unicode=True, sort_keys=False)
    print(f"Wrote part for PR #{pr_number} by {author}")
    return True


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("parts_dir")
    parser.add_argument("--changelog-file", default="Resources/Changelog/CMU.yml")
    parser.add_argument("--category", default=CATEGORY_MAIN)
    args = parser.parse_args()

    token = os.environ["GITHUB_TOKEN"]
    repo = os.environ["GITHUB_REPOSITORY"]
    start = os.environ.get("START_DATE") or None

    current = load_changelog(args.changelog_file)
    entries = current.get("Entries", [])
    since = get_scan_start(entries, repo, start)
    seen_urls = get_seen_urls(entries, args.parts_dir)

    print(f"Fetching PRs from {repo} merged since {since}")
    sess = make_session(token)
    base = os.environ.get("GITHUB_BASE_BRANCH")
    prs = get_merged_prs(sess, repo, since, base)
    print(f"Found {len(prs)} merged PRs")

    written = 0
    for item in prs:
        item_url = item.get("html_url")
        if item_url in seen_urls:
            print(f"PR #{item['number']}: already present in changelog, skipping")
            continue

        pr = fetch_pr(sess, repo, item["number"])
        if base and pr.get("base", {}).get("ref") != base:
            continue
        body = pr.get("body") or ""
        result = parse_cl_block(body, pr["user"]["login"])
        if result is None:
            print(f"PR #{pr['number']}: no non-empty :cl: block, skipping")
            continue

        author, changes = result
        merged_at = pr.get("merged_at")
        if not merged_at:
            print(f"PR #{pr['number']}: no merged_at value, skipping")
            continue

        time = format_time(parse_time(merged_at))
        labels = [label["name"] for label in pr.get("labels", []) if label.get("name")]
        if write_part(
            args.parts_dir,
            pr["number"],
            author,
            changes,
            time,
            pr["html_url"],
            args.category,
            labels,
        ):
            seen_urls.add(pr["html_url"])
            written += 1

    print(f"Done. Wrote {written} parts from {len(prs)} PRs.")


if __name__ == "__main__":
    main()
# CMU14 End
