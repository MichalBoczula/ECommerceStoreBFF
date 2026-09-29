#!/usr/bin/env python3
"""Report test counters from the single TRX produced by the BFF verification run."""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

if len(sys.argv) != 3:
    raise SystemExit("Usage: summarize-trx.py RESULTS_DIR SUMMARY_FILE")

directory, summary_file = Path(sys.argv[1]), Path(sys.argv[2])
files = list(directory.rglob("*.trx"))
if len(files) != 1:
    raise SystemExit(f"Expected one TRX, found {len(files)} in {directory}")

root = ET.parse(files[0]).getroot()
counters = root.find(".//{*}Counters")
if counters is None:
    raise SystemExit(f"Missing test counters in {files[0]}")

total, passed, failed, skipped = (
    int(counters.get(name, "0")) for name in ("total", "passed", "failed", "notExecuted")
)
summary = (
    "| Suite | Total | Passed | Failed | Skipped |\n"
    "| --- | ---: | ---: | ---: | ---: |\n"
    f"| BFF integration | {total} | {passed} | {failed} | {skipped} |\n"
)
print(summary)
with summary_file.open("a", encoding="utf-8") as output:
    output.write(summary)

if total == 0 or passed == 0 or failed != 0 or passed + failed + skipped != total:
    raise SystemExit(f"Invalid test result: total={total}, passed={passed}, failed={failed}, skipped={skipped}")
