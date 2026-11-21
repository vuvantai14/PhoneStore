"""Prepare a new Web/API Docker build directory from tracked CI checkout files."""
import json
from pathlib import Path
import shutil
import subprocess
import sys


root = Path(__file__).resolve().parents[2]
destination = Path(sys.argv[1]).resolve()
destination.mkdir(exist_ok=False)
inputs = ["Dockerfile", ".dockerignore", "global.json"]
inputs.extend(f"src/PhoneStore.{project}" for project in ("Web", "Api", "Business", "Data", "Models"))
files = subprocess.check_output(["git", "ls-files", "-z", "--", *inputs], cwd=root)
for name in files.decode().split("\0"):
    if not name:
        continue
    source = root / name
    target = destination / name
    parts = {part.lower() for part in Path(name).parts}
    if parts & {"bin", "obj", "uploads"} or source.name.lower().startswith(".env"):
        continue
    if source.suffix.lower() in {".bak", ".mdf", ".ndf", ".ldf", ".db", ".sqlite", ".bacpac", ".pfx"}:
        continue
    if source.is_symlink() or not source.is_file():
        raise SystemExit(f"Unexpected Docker input: {name}")
    target.parent.mkdir(parents=True, exist_ok=True)
    if source.match("appsettings*.json"):
        settings = json.loads(source.read_text(encoding="utf-8-sig"))
        settings = {key: value for key, value in settings.items() if key.casefold() != "connectionstrings"}
        target.write_text(json.dumps(settings, ensure_ascii=False, indent=2), encoding="utf-8")
    else:
        shutil.copyfile(source, target)
