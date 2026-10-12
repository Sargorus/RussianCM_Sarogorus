import datetime as dt
import json
import os
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock, patch

import requests
import yaml

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

import actions_changelogs_since_last_run as discord
import update_changelog as assembler
import update_changelog_parts as parts
from changelog_discord_state import FileStateStore, GitHubStateStore, empty_state
from changelog_translation import RussianTranslator, TranslationError, TranslationRateLimitError, protect, restore, retry_after_seconds
from changelog_translation_cache import load_translation_cache, save_translation_cache


def entry(number=1, source="CMU", timestamp="2026-10-01T12:00:00Z", message="Исправлены ошибки."):
    e = {"author": "author", "id": number, "time": timestamp,
         "url": f"https://github.com/example/game/pull/{number}",
         "changes": [{"type": "Fix", "message": message}], "source": source}
    e["key"] = discord.delivery_key(source, e)
    return e


class AssemblyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.file = self.root / "CMU.yml"
        self.parts = self.root / "Parts"
        self.parts.mkdir()
        self.save(self.file, {"Name": "CMU", "Order": -1, "Entries": [entry(30)]})

    def save(self, path, data):
        path.write_text(yaml.safe_dump(data, allow_unicode=True, sort_keys=False), encoding="utf-8")

    def add_part(self, name="part.yml", **changes):
        part = entry(31)
        part.pop("id")
        part.update(changes)
        self.save(self.parts / name, part)

    def test_real_cli_regression_no_undefined_variables_or_duplicate_append(self):
        self.add_part()
        # Use the imported function so the bundled Python path works on Windows too.
        with patch.object(sys, "argv", ["update_changelog", str(self.file), str(self.parts), "--category", "CMU"]):
            assembler.main()
        data = assembler.load_yaml(self.file)
        self.assertEqual([e["id"] for e in data["Entries"]], [30, 31])
        self.assertEqual(data["Name"], "CMU")
        self.assertEqual(data["Order"], -1)
        self.assertFalse(list(self.parts.glob("*.yml")))

    def test_duplicate_url_ignores_changed_timestamp_and_author_and_consumes_part(self):
        self.add_part(url=entry(30)["url"], author="other", time="2026-10-02T00:00:00Z")
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertEqual(len(assembler.load_yaml(self.file)["Entries"]), 1)
        self.assertFalse(list(self.parts.glob("*.yml")))

    def test_existing_duplicate_url_merges_distinct_changes(self):
        a, b = entry(30), entry(31, message="Добавлены карты.")
        b["url"] = a["url"]
        self.save(self.file, {"Entries": [a, b]})
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        entries = assembler.load_yaml(self.file)["Entries"]
        self.assertEqual(len(entries), 1)
        self.assertEqual(len(entries[0]["changes"]), 2)
        self.assertEqual(entries[0]["id"], 31)

    def test_colliding_upstream_ids_receive_new_unique_ids(self):
        a, b = entry(30), entry(31)
        b["id"] = 30
        self.save(self.file, {"Entries": [a, b]})
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertEqual([e["id"] for e in assembler.load_yaml(self.file)["Entries"]], [30, 31])

    def test_duplicate_url_carrying_another_pr_id_does_not_create_collision(self):
        a, b, duplicate = entry(30), entry(31), entry(31)
        duplicate["url"] = a["url"]
        self.save(self.file, {"Entries": [a, b, duplicate]})
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        entries = assembler.load_yaml(self.file)["Entries"]
        self.assertEqual(len(entries), 2)
        self.assertEqual(len({e["id"] for e in entries}), 2)
        self.assertEqual(max(e["id"] for e in entries), 32)

    def test_invalid_part_leaves_changelog_and_all_parts_intact(self):
        self.add_part("a.yml")
        self.add_part("b.yml", changes=[{"type": "Fix", "message": 123}])
        before = self.file.read_bytes()
        with self.assertRaises(ValueError):
            assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertEqual(self.file.read_bytes(), before)
        self.assertEqual(len(list(self.parts.glob("*.yml"))), 2)

    def test_other_category_is_retained(self):
        self.add_part(category="Admin")
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertTrue((self.parts / "part.yml").exists())

    def test_no_op_after_first_assembly_preserves_bytes(self):
        self.add_part()
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        before = self.file.read_bytes()
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertEqual(self.file.read_bytes(), before)

    def test_pruning_never_renumbers_survivors(self):
        self.save(self.file, {"Entries": [entry(30), entry(31), entry(32)]})
        self.add_part(url=entry(33)["url"])
        with patch.object(assembler, "MAX_ENTRIES", 2):
            assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertEqual([e["id"] for e in assembler.load_yaml(self.file)["Entries"]], [32, 33])

    def test_pruning_old_upstream_high_id_never_reuses_issued_ids(self):
        self.save(self.file, {"Entries": [entry(1000, timestamp="2020-01-01"), entry(31), entry(32)]})
        with patch.object(assembler, "MAX_ENTRIES", 2):
            assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.add_part(url=entry(33)["url"])
        assembler.assemble(str(self.file), str(self.parts), "CMU")
        self.assertEqual(max(e["id"] for e in assembler.load_yaml(self.file)["Entries"]), 1001)

    def test_all_categories_consume_tagged_parts_into_correct_files(self):
        for category, filename in assembler.CHANGELOG_CATEGORIES.items():
            if filename != "CMU":
                self.save(self.root / f"{filename}.yml", {"Entries": []})
            self.add_part(f"{category}.yml", category=category)
        with patch.object(sys, "argv", ["assemble", str(self.file), str(self.parts), "--category", "CMU", "--all"]):
            assembler.main()
        for filename in assembler.CHANGELOG_CATEGORIES.values():
            expected = 2 if filename == "CMU" else 1
            self.assertEqual(len(assembler.load_yaml(str(self.root / f"{filename}.yml"))["Entries"]), expected)
        self.assertFalse(list(self.parts.glob("*.yml")))


class SelectionTests(unittest.TestCase):
    def test_identity_survives_upstream_renumber_and_timestamp_rewrite(self):
        old = entry()
        new = entry()
        new.update(id=900, time="2026-10-04", author="new author")
        self.assertEqual(discord.diff_changelog({"Entries": [old]}, {"Entries": [new]}), [])

    def test_same_pr_number_from_another_repository_is_new(self):
        old, new = entry(), entry()
        new["url"] = "https://github.com/upstream/game/pull/1"
        self.assertEqual(len(discord.diff_changelog({"Entries": [old]}, {"Entries": [new]})), 1)

    def test_sections_have_independent_identity(self):
        self.assertNotEqual(entry(source="CMU")["key"], entry(source="RMC14")["key"])

    def test_late_upstream_entry_with_old_time_is_delivered(self):
        old = entry(1, timestamp="2025-01-01")
        state = discord.initial_state([old], now=dt.datetime(2026, 10, 6, tzinfo=dt.timezone.utc))
        imported = entry(2, timestamp="2025-02-01")
        self.assertEqual(discord.select_entries([old, imported], state), [imported])

    def test_backfill_includes_baseline_but_never_delivered_entries(self):
        old = entry(1, timestamp="2026-08-01")
        done = entry(2, timestamp="2026-08-02")
        state = discord.initial_state([old, done], now=dt.datetime(2026, 10, 6, tzinfo=dt.timezone.utc))
        state["delivered"][done["key"]] = "confirmed"
        self.assertEqual(discord.select_entries([old, done], state, "2026-07-01"), [old])

    def test_manual_timestamp_normalization(self):
        a, b = entry(), entry()
        a["url"] = b["url"] = None
        b["time"] = "2026-10-01T12:00:00.0000000+00:00"
        self.assertEqual(discord.delivery_key("CMU", a), discord.delivery_key("CMU", b))

    def test_different_manual_entries_from_same_author_and_time_are_not_lost(self):
        a, b = entry(), entry(message="Добавлены карты.")
        a["url"] = b["url"] = None
        self.assertNotEqual(discord.delivery_key("CMU", a), discord.delivery_key("CMU", b))

    def test_undated_historical_entry_has_stable_content_identity(self):
        a, b = entry(), entry()
        a.pop("time")
        b.pop("time")
        a["url"] = b["url"] = None
        b["id"] = 500
        self.assertEqual(discord.delivery_key("Maps", a), discord.delivery_key("Maps", b))
        self.assertEqual(discord.entry_date(a).year, 1)

    def test_real_files_all_sections_and_invalid_historical_dates(self):
        entries = discord.load_entries(discord.DEFAULT_FILES)
        self.assertEqual({e["source"] for e in entries}, set(discord.SECTION_NAMES))
        self.assertEqual(len({e["key"] for e in entries}), len(entries))


class TranslationTests(unittest.TestCase):
    def test_numbers_urls_code_and_terms_are_preserved(self):
        source = "Increased FOB speed by 30% using `CMU.CVar` https://example.org/test."
        masked, tokens = protect(source, ["FOB"])
        self.assertNotIn("30%", masked)
        self.assertEqual(restore(masked, tokens), source)
        with self.assertRaises(TranslationError):
            restore(masked.replace("__KEEP_0__", ""), tokens)

    def test_preserved_english_code_is_not_mistaken_for_untranslated_prose(self):
        translator = RussianTranslator({}, request=Mock(return_value=["Исправлена обработка __KEEP_0__."]))
        self.assertEqual(translator.translate(["Fixed `the energy sword` handling."]),
                         ["Исправлена обработка `the energy sword`."])

    def test_russian_entries_do_not_call_provider(self):
        request = Mock(side_effect=AssertionError("should not translate"))
        translator = RussianTranslator({}, request=request)
        source = "Исправлена связь GOVFOR на FOB."
        self.assertEqual(translator.translate([source]), [source])
        request.assert_not_called()

    def test_mixed_language_is_translated_and_cached(self):
        request = Mock(return_value=["Исправлено восстановление связи."])
        cache = {}
        translator = RussianTranslator(cache, request=request)
        source = "Исправлен radio connection recovery."
        self.assertEqual(translator.translate([source, source]), ["Исправлено восстановление связи."] * 2)
        self.assertEqual(translator.translate([source]), ["Исправлено восстановление связи."])
        request.assert_called_once()

    def test_bad_translation_is_not_cached_or_sent(self):
        for translated in ["", "Fixed the radio connection.", "Русский текст with untranslated English prose"]:
            cache = {}
            translator = RussianTranslator(cache, request=Mock(return_value=[translated]))
            with self.assertRaises(TranslationError):
                translator.translate(["Fixed the radio connection."])
            self.assertEqual(cache, {})

    def test_partial_translation_batch_must_not_mutate_cache(self):
        cache = {}
        translator = RussianTranslator(cache, request=Mock(return_value=["Связь исправлена."]))
        with self.assertRaises(TranslationError):
            translator.translate(["Fixed radio.", "Added a map."])
        self.assertEqual(cache, {})

    def test_dropped_protected_fact_fails_validation(self):
        translator = RussianTranslator({}, request=Mock(return_value=["Скорость повышена."]))
        with self.assertRaises(TranslationError):
            translator.translate(["Increased speed by 30%."])

    def test_missing_secret_fails_without_english_fallback(self):
        with patch.dict(os.environ, {}, clear=True):
            with self.assertRaises(TranslationError):
                RussianTranslator({}).translate(["Fixed radio."])

    def test_mistral_response_ids_restore_input_order(self):
        response = Mock(status_code=200)
        response.json.return_value = {"choices": [{"finish_reason": "stop", "message": {"content": json.dumps(
            {"messages": [{"id": 1, "text": "Добавлена карта."}, {"id": 0, "text": "Связь исправлена."}]})}}]}
        with patch.dict(os.environ, {"MISTRAL_API_KEY": "test"}), patch("changelog_translation.requests.post", return_value=response):
            self.assertEqual(RussianTranslator({}).translate(["Fixed radio.", "Added a map."]),
                             ["Связь исправлена.", "Добавлена карта."])


class FakeClock:
    def __init__(self):
        self.now = 0.0
        self.sleeps = []

    def monotonic(self):
        return self.now

    def sleep(self, seconds):
        self.sleeps.append(seconds)
        self.now += seconds


def service_response(status=200, translations=None, headers=None, error=None):
    response = Mock(status_code=status, headers=headers or {})
    if status == 200:
        response.json.return_value = {"choices": [{"finish_reason": "stop", "message": {"content": json.dumps(
            {"messages": [{"id": i, "text": value} for i, value in enumerate(translations or ["Связь исправлена."])]})}}]}
    else:
        response.json.return_value = error or {"message": "Rate limit exceeded"}
    return response


class TranslationRetryTests(unittest.TestCase):
    def setUp(self):
        self.clock = FakeClock()
        self.addCleanup(patch.stopall)
        patch.dict(os.environ, {"MISTRAL_API_KEY": "test", "CHANGELOG_TRANSLATION_INTERVAL_SECONDS": "15"}).start()
        patch("changelog_translation.time.monotonic", side_effect=self.clock.monotonic).start()
        patch("changelog_translation.time.sleep", side_effect=self.clock.sleep).start()

    def test_429_without_header_waits_for_minute_then_recovers(self):
        with patch("changelog_translation.requests.post", side_effect=[service_response(429), service_response()]) as post:
            self.assertEqual(RussianTranslator({}).translate(["Fixed radio."]), ["Связь исправлена."])
        self.assertEqual(post.call_count, 2)
        self.assertEqual(self.clock.sleeps, [60])

    def test_retry_after_seconds_is_respected(self):
        with patch("changelog_translation.requests.post", side_effect=[
                service_response(429, headers={"Retry-After": "75"}), service_response()]):
            RussianTranslator({}).translate(["Fixed radio."])
        self.assertEqual(self.clock.sleeps, [75])

    def test_retry_after_http_date_and_invalid_values(self):
        now = dt.datetime(2026, 10, 6, 0, 0, 0, tzinfo=dt.timezone.utc)
        self.assertEqual(retry_after_seconds("Tue, 06 Oct 2026 00:01:30 GMT", now), 90)
        self.assertEqual(retry_after_seconds("-1", now), 0)
        for value in [None, "bad", "NaN", "inf"]:
            self.assertIsNone(retry_after_seconds(value, now))

    def test_server_wait_larger_than_budget_is_not_shortened(self):
        with patch("changelog_translation.requests.post", return_value=service_response(429, headers={"Retry-After": "7200"})) as post:
            with self.assertRaisesRegex(TranslationRateLimitError, "retry_after=7200"):
                RussianTranslator({}).translate(["Fixed radio."])
        post.assert_called_once()
        self.assertEqual(self.clock.sleeps, [])

    def test_persistent_429_has_bounded_wait_and_clear_diagnostic(self):
        with patch("changelog_translation.requests.post", return_value=service_response(429)) as post:
            with self.assertRaisesRegex(TranslationRateLimitError, "API/Limits"):
                RussianTranslator({}).translate(["Fixed radio."])
        self.assertLessEqual(post.call_count, 6)
        self.assertLessEqual(sum(self.clock.sleeps), 600)
        self.assertGreaterEqual(sum(self.clock.sleeps), 60)

    def test_explicit_monthly_quota_does_not_retry_or_echo_service_body(self):
        response = service_response(429, error={"message": "Monthly spending limit reached SECRET"})
        with patch("changelog_translation.requests.post", return_value=response) as post:
            with self.assertRaisesRegex(TranslationRateLimitError, "monthly quota") as caught:
                RussianTranslator({}).translate(["Fixed radio."])
        self.assertNotIn("SECRET", str(caught.exception))
        post.assert_called_once()
        self.assertEqual(self.clock.sleeps, [])

    def test_batches_are_paced_and_max_tokens_is_bounded(self):
        first = service_response(translations=["Связь исправлена __KEEP_0__."] * 6)
        second = service_response(translations=["Связь исправлена __KEEP_0__."])
        with patch("changelog_translation.requests.post", side_effect=[first, second]) as post:
            translated = RussianTranslator({}).translate([f"Fixed radio {i}." for i in range(7)])
        self.assertEqual(len(translated), 7)
        self.assertEqual(self.clock.sleeps, [15])
        self.assertTrue(all(256 <= call.kwargs["json"]["max_tokens"] <= 4096 for call in post.call_args_list))

    def test_long_notes_are_split_by_text_size_not_only_count(self):
        counts = []
        def request(messages):
            counts.append(len(messages))
            return ["Связь исправлена __KEEP_0__." for _ in messages]
        originals = [f"Fixed radio {i}. " + "Some description. " * 60 for i in range(3)]
        self.assertEqual(len(RussianTranslator({}, request=request).translate(originals)), 3)
        self.assertEqual(counts, [1, 1, 1])

    def test_progress_is_saved_before_next_batch_failure_and_resumed(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "translations.json"
            cache = {}
            originals = [f"Fixed radio {i}." for i in range(7)]
            request = Mock(side_effect=[["Связь исправлена __KEEP_0__."] * 6, TranslationRateLimitError("429")])
            translator = RussianTranslator(cache, request=request, checkpoint=lambda: save_translation_cache(target, cache))
            with self.assertRaises(TranslationRateLimitError):
                translator.translate(originals)
            recovered = load_translation_cache(target)
            self.assertEqual(len(recovered), 6)
            request = Mock(return_value=["Связь исправлена __KEEP_0__."])
            translated = RussianTranslator(recovered, request=request).translate(originals)
            self.assertEqual(len(translated), 7)
            request.assert_called_once()
            self.assertEqual(len(request.call_args.args[0]), 1)
            self.assertEqual(set(json.loads(target.read_text(encoding="utf-8"))), {"version", "translations"})

    def test_preview_failure_saves_translations_but_never_changes_delivery_state(self):
        with tempfile.TemporaryDirectory() as directory:
            journal_path = Path(directory) / "journal.json"
            cache_path = Path(directory) / "translations.json"
            store = FileStateStore(journal_path)
            store.save(empty_state())
            before = journal_path.read_bytes()
            selected = [entry(i + 1, message=f"Fixed radio {i}.") for i in range(7)]
            request = Mock(side_effect=[["Связь исправлена __KEEP_0__."] * 6, TranslationRateLimitError("429")])
            with patch.object(sys, "argv", ["publisher", "--dry-run", "--state-file", str(journal_path),
                    "--translation-cache", str(cache_path)]), \
                    patch.object(discord, "load_entries", return_value=selected), \
                    patch.object(RussianTranslator, "request_mistral", request), \
                    patch.object(discord, "send_discord_webhook", side_effect=AssertionError("No Discord sends")):
                with self.assertRaises(TranslationRateLimitError):
                    discord.main()
            self.assertEqual(journal_path.read_bytes(), before)
            self.assertEqual(len(load_translation_cache(cache_path)), 6)

    def test_invalid_cache_is_not_delivery_reset(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory) / "bad.json"
            target.write_text('{"version":1,"delivered":{}}', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "delivery journal"):
                load_translation_cache(target)



class DeliveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.store = FileStateStore(Path(self.temp.name) / "journal.json")
        self.state = empty_state()
        self.state["pending"] = {"keys": ["key"], "chunks": ["Первое", "Второе"], "next": 0, "receipts": []}
        self.store.save(self.state)

    def test_partial_rejection_resumes_confirmed_payload_without_duplicates(self):
        sender = Mock(side_effect=["receipt1", discord.DeliveryRejected("HTTP 400")])
        with self.assertRaises(discord.DeliveryRejected):
            discord.publish_pending(self.state, self.store, sender)
        recovered = self.store.load()
        self.assertEqual(recovered["pending"]["next"], 1)
        self.assertEqual(recovered["delivered"], {})
        sender = Mock(return_value="receipt2")
        discord.publish_pending(recovered, self.store, sender)
        sender.assert_called_once_with("Второе")
        self.assertIn("key", self.store.load()["delivered"])
        self.assertIsNone(self.store.load()["pending"])

    def test_timeout_pauses_future_runs_until_operator_verifies_channel(self):
        with self.assertRaises(RuntimeError):
            discord.publish_pending(self.state, self.store, Mock(side_effect=RuntimeError("timeout")))
        recovered = self.store.load()
        sender = Mock()
        with self.assertRaises(RuntimeError):
            discord.publish_pending(recovered, self.store, sender)
        sender.assert_not_called()
        discord.resolve_ambiguous(recovered, "delivered", self.store)
        sender.return_value = "receipt2"
        discord.publish_pending(recovered, self.store, sender)
        sender.assert_called_once_with("Второе")

    def test_operator_retry_after_confirmed_absence(self):
        self.state["pending"]["in_flight"] = 0
        discord.resolve_ambiguous(self.state, "retry", self.store)
        self.assertIsNone(self.store.load()["pending"]["in_flight"])
        self.assertEqual(self.store.load()["pending"]["next"], 0)

    def test_corrupt_journal_is_not_treated_as_empty(self):
        self.store.path.write_text("{}", encoding="utf-8")
        with self.assertRaises(ValueError):
            self.store.load()

    def test_discord_wait_confirmation_and_mentions_disabled(self):
        response = Mock(status_code=200)
        response.json.return_value = {"id": "123"}
        with patch.object(discord.requests, "post", return_value=response) as post:
            self.assertEqual(discord.send_discord_webhook("Текст", "https://example.invalid/token"), "123")
        self.assertEqual(post.call_args.kwargs["params"], {"wait": "true"})
        self.assertEqual(post.call_args.kwargs["json"]["allowed_mentions"], {"parse": []})

    def test_discord_429_retries_but_timeout_does_not(self):
        rate_limit, success = Mock(status_code=429), Mock(status_code=200)
        rate_limit.json.return_value = {"retry_after": 0}
        success.json.return_value = {"id": "id"}
        with patch.object(discord.requests, "post", side_effect=[rate_limit, success]) as post:
            discord.send_discord_webhook("Текст", "https://example.invalid/token")
            self.assertEqual(post.call_count, 2)
        with patch.object(discord.requests, "post", side_effect=requests.Timeout("SECRET_URL")) as post:
            with self.assertRaisesRegex(RuntimeError, "paused") as context:
                discord.send_discord_webhook("Текст", "https://example.invalid/token")
            self.assertNotIn("SECRET_URL", str(context.exception))
            self.assertEqual(post.call_count, 1)

    def test_long_messages_preserve_all_text_and_repeat_links(self):
        message = ("Очень длинная запись 🛠️ " * 300).rstrip()
        chunks = discord.entry_chunks(entry(message=message))
        self.assertGreater(len(chunks), 1)
        for chunk in chunks:
            self.assertLessEqual(discord.discord_length(chunk), 2000)
            self.assertIn("https://github.com/example/game/pull/1", chunk)
        fragments = []
        for chunk in chunks:
            for line in chunk.splitlines()[1:-1]:
                fragments.append(line[len("🔧 "):])
        self.assertEqual("".join(fragments), message)

    def test_dry_run_has_no_discord_or_journal_writes(self):
        self.state["pending"] = None
        self.store.save(self.state)
        before = self.store.path.read_bytes()
        output = Path(self.temp.name) / "preview.md"
        with patch.object(sys, "argv", ["publisher", "--dry-run", "--state-file", str(self.store.path), "--output", str(output)]), \
                patch.object(discord, "load_entries", return_value=[entry()]), \
                patch.object(discord.requests, "post", side_effect=AssertionError("network forbidden")):
            discord.main()
        self.assertEqual(self.store.path.read_bytes(), before)
        self.assertIn("Исправлены ошибки.", output.read_text(encoding="utf-8"))

    def test_translation_failure_never_marks_entry_delivered(self):
        self.state["pending"] = None
        self.store.save(self.state)
        with patch.object(sys, "argv", ["publisher", "--state-file", str(self.store.path)]), \
                patch.dict(os.environ, {"DISCORD_WEBHOOK_URL": "dummy"}), \
                patch.object(discord, "load_entries", return_value=[entry(message="Fixed radio.")]), \
                patch.object(discord.RussianTranslator, "translate", side_effect=TranslationError("no translation")):
            with self.assertRaises(TranslationError):
                discord.main()
        self.assertEqual(self.store.load()["delivered"], {})
        self.assertIsNone(self.store.load()["pending"])

    def test_missing_journal_on_existing_branch_does_not_reset(self):
        session = Mock()
        missing, branch = Mock(status_code=404), Mock(status_code=200)
        session.request.side_effect = [missing, branch]
        with self.assertRaisesRegex(RuntimeError, "refusing to reset"):
            GitHubStateStore("repo/name", "test", session=session).load()


class ParserTests(unittest.TestCase):
    def test_equal_pr_numbers_from_two_repositories_do_not_collide(self):
        with tempfile.TemporaryDirectory() as directory:
            for repo in ["local/game", "upstream/game"]:
                self.assertTrue(parts.write_part(directory, 12, "author", [], "2026-10-01",
                                                 f"https://github.com/{repo}/pull/12", "CMU", []))
            self.assertEqual(len(list(Path(directory).glob("*.yml"))), 2)

    def test_template_comments_and_empty_blocks_are_ignored(self):
        body = "<!-- :cl:\nadd: Added fun! -->\n:cl: Author\n- fix: Исправлена связь.\n"
        self.assertEqual(parts.parse_cl_block(body, "fallback"),
                         ("Author", [{"type": "Fix", "message": "Исправлена связь."}]))
        self.assertIsNone(parts.parse_cl_block(":cl:\n- add: Added fun!", "fallback"))

    def test_scan_uses_local_repo_cursor_not_newer_upstream_time(self):
        local, upstream = entry(), entry(2, timestamp="2026-10-05")
        upstream["url"] = "https://github.com/upstream/game/pull/2"
        self.assertEqual(parts.get_scan_start([local, upstream], "example/game", None), "2026-09-24T12:00:00Z")

    def test_search_filters_target_base_and_has_timeout(self):
        sess = Mock()
        sess.get.return_value.json.return_value = {"items": []}
        parts.get_merged_prs(sess, "repo/name", "2026-10-01", "master")
        self.assertIn("base:master", sess.get.call_args.kwargs["params"]["q"])
        self.assertEqual(sess.get.call_args.kwargs["timeout"], 30)


if __name__ == "__main__":
    unittest.main()
