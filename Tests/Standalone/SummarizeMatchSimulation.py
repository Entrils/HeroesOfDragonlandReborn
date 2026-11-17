"""Summarize flushed M52 evidence; never converts timeouts into successful matches."""
import collections
import json
import pathlib
import sys

directory = pathlib.Path(sys.argv[1])
summary = json.loads((directory / "summary.json").read_text(encoding="utf-8-sig"))
actors = {}
events = collections.Counter()
attempt = 0
rounds = []
with (directory / "events.jsonl").open(encoding="utf-8-sig") as stream:
    for line in stream:
        try:
            record = json.loads(line)
        except json.JSONDecodeError:
            events["incomplete-json-line"] += 1
            continue
        kind = record["kind"]
        events[kind] += 1
        if kind == "round-start":
            attempt += 1
        if kind == "round-summary":
            rounds.append(record)
        if record.get("actor"):
            key = f"attempt-{attempt}/{record['actor']}"
            actor = actors.setdefault(key, {"team": record.get("team"), "kills": 0, "deaths": 0, "stuck": 0, "highest_level": 1})
            if kind == "kill":
                actor["kills"] += 1
            elif kind == "death":
                actor["deaths"] += 1
            elif kind == "stuck-ai":
                actor["stuck"] += 1
            elif kind == "level":
                actor["highest_level"] = max(actor["highest_level"], int(record["message"]))
teams = {}
for actor in actors.values():
    team = teams.setdefault(actor["team"], {"kills": 0, "deaths": 0, "stuck": 0, "highest_level": 1})
    for field in ("kills", "deaths", "stuck"):
        team[field] += actor[field]
    team["highest_level"] = max(team["highest_level"], actor["highest_level"])
result = {"summary": summary, "events": dict(events), "rounds": rounds, "actors": actors, "teams": teams,
          "scope": "Local single-runner evidence only; timeouts/interruption are failures."}
(directory / "analysis.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps({"status": summary["message"], "seconds": summary["seconds"], "counters": summary["counters"], "events": dict(events), "teams": teams}, indent=2))
