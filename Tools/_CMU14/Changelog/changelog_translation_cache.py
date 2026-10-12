"""Reusable translation work only; never contains Discord delivery acknowledgements."""

import json
from pathlib import Path


def load_translation_cache(path):
    target = Path(path)
    if not target.exists():
        return {}
    data = json.loads(target.read_text(encoding="utf-8"))
    if (not isinstance(data, dict) or data.get("version") != 1
            or not isinstance(data.get("translations"), dict)
            or any(not isinstance(k, str) or not isinstance(v, str)
                   for k, v in data["translations"].items())):
        raise ValueError("Invalid translation cache; Discord delivery journal was not changed")
    return data["translations"]


def save_translation_cache(path, translations):
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_suffix(target.suffix + ".tmp")
    temporary.write_text(json.dumps({"version": 1, "translations": translations}, ensure_ascii=False) + "\n",
                         encoding="utf-8", newline="\n")
    temporary.replace(target)
