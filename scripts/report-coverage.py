#!/usr/bin/env python3
"""Report handwritten BFF line coverage and reject generated code in the report."""

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

if len(sys.argv) != 3:
    raise SystemExit("Usage: report-coverage.py RESULTS_DIR SUMMARY_FILE")

directory, summary_file = Path(sys.argv[1]), Path(sys.argv[2])
reports = list(directory.rglob("coverage.cobertura.xml"))
if len(reports) != 1:
    raise SystemExit(f"Expected one Cobertura report, found {len(reports)} in {directory}")

root = ET.parse(reports[0]).getroot()
files = [item.get("filename", "").replace("\\", "/") for item in root.findall(".//class")]
generated = [name for name in files if "/Generated/" in f"/{name}"]
if generated:
    raise SystemExit(f"Generated Kiota code found in coverage: {generated[:5]}")

handwritten = [name for name in files if "/ECommerceStoreBFF." in f"/{name}"]
if not any(name.endswith("/DependencyInjection.cs") for name in handwritten):
    raise SystemExit("Infrastructure DependencyInjection.cs is missing from coverage scope")

valid = int(root.get("lines-valid", "0"))
covered = int(root.get("lines-covered", "0"))
if not (0 < valid and 0 <= covered <= valid):
    raise SystemExit(f"Invalid handwritten coverage: {covered}/{valid}")

rate = 100 * covered / valid
summary = f"\n## Handwritten BFF coverage\n\n{covered}/{valid} lines ({rate:.1f}%). Generated Kiota sources excluded.\n"
print(summary)
with summary_file.open("a", encoding="utf-8") as output:
    output.write(summary)
