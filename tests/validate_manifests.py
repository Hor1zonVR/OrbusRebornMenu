"""Validate proposed RebornMenu manifests using JSON Schema Draft 2020-12."""
from __future__ import annotations

import json
from pathlib import Path
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
SCHEMA_PATH = ROOT / "schemas" / "rebornmenu.schema.json"
MANIFESTS = sorted((ROOT / "examples").glob("*/rebornmenu.json"))

def main() -> None:
    schema = json.loads(SCHEMA_PATH.read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(schema)
    validator = Draft202012Validator(schema)
    assert MANIFESTS, "At least one example manifest is required"

    for path in MANIFESTS:
        data = json.loads(path.read_text(encoding="utf-8"))
        errors = list(validator.iter_errors(data))
        if errors:
            details = "\n".join(
                f"{'/'.join(str(x) for x in e.absolute_path)}: {e.message}"
                for e in sorted(errors, key=lambda e: str(e.absolute_path))
            )
            raise AssertionError(f"{path.relative_to(ROOT)} is invalid:\n{details}")

        tab_ids = [x["id"] for x in data["tabs"]]
        assert len(tab_ids) == len(set(tab_ids)), f"Duplicate tab ID in {path}"
        for tab in data["tabs"]:
            control_ids = [x["id"] for x in tab["controls"]]
            assert len(control_ids) == len(set(control_ids)), (
                f"Duplicate control ID in {path}: {tab['id']}"
            )
        print(f"PASS: {path.relative_to(ROOT)}")

if __name__ == "__main__":
    main()
