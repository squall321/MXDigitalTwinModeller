"""
Apply patches from a workflow output JSON file.

Reads JSON of shape:
  result.{section}.files[] with action=create/edit/append,
  path, old_string, new_string, content.
"""
import json
import sys
import os

OUT_PATH = sys.argv[1] if len(sys.argv) > 1 else (
    r"C:\Users\Sonic\AppData\Local\Temp\claude\d--MXDigitalTwinModeller"
    r"\1c7b49ff-c49d-4bd8-a1ce-4f2c20933184\tasks\w6owy568b.output"
)

with open(OUT_PATH, encoding="utf-8") as f:
    data = json.load(f)

result = data.get("result", {})
sections = [k for k, v in result.items() if isinstance(v, dict) and "files" in v]
total_files = 0
total_edits = 0
errors = []

for sec in sections:
    sec_data = data.get("result", {}).get(sec, {})
    files = sec_data.get("files") or []
    print(f"\n=== {sec} ({len(files)} file ops) ===")
    for f_entry in files:
        action = f_entry.get("action")
        path = f_entry.get("path")
        if not path:
            errors.append(f"{sec}: missing path")
            continue
        # Normalize backslashes from JSON to OS
        if not os.path.isabs(path):
            errors.append(f"{sec}: non-absolute path {path}")
            continue

        if action == "create":
            content = f_entry.get("content", "")
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w", encoding="utf-8", newline="") as out:
                out.write(content)
            print(f"  [CREATE] {path} ({len(content)} bytes)")
            total_files += 1
        elif action == "edit":
            old = f_entry.get("old_string", "")
            new = f_entry.get("new_string", "")
            if not os.path.exists(path):
                errors.append(f"{sec}: edit on missing file {path}")
                continue
            with open(path, "r", encoding="utf-8") as fh:
                body = fh.read()
            if old not in body:
                errors.append(f"{sec}: old_string not found in {path} (len={len(old)})")
                # Print first 80 chars of old_string for debugging
                print(f"    [SKIP] {path}: anchor missing — first 80 of old_string:")
                print(f"           >>> {old[:80]!r}")
                continue
            new_body = body.replace(old, new, 1)
            with open(path, "w", encoding="utf-8", newline="") as fh:
                fh.write(new_body)
            print(f"  [EDIT  ] {path} (-{len(old)} +{len(new)})")
            total_edits += 1
        elif action == "append":
            new = f_entry.get("new_string") or f_entry.get("content", "")
            with open(path, "a", encoding="utf-8") as fh:
                fh.write(new)
            print(f"  [APPEND] {path} (+{len(new)})")
            total_edits += 1
        else:
            errors.append(f"{sec}: unknown action {action} for {path}")

print("\n" + "=" * 50)
print(f"Created: {total_files}, Edits: {total_edits}, Errors: {len(errors)}")
for e in errors:
    print(f"  ! {e}")

sys.exit(1 if errors else 0)
