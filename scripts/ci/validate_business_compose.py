"""Validate the business Compose model offline, using only synthetic inputs."""
import json
import os
from pathlib import Path
import secrets
import subprocess


def require(condition, message):
    if not condition:
        raise SystemExit(message)


root = Path(__file__).resolve().parents[2]
project = "phonestore-restore-c2"
env = {**os.environ, "MSSQL_SA_PASSWORD": secrets.token_urlsafe(32),
       "PRODUCT_UPLOADS_PATH": str(root / "src/PhoneStore.Web/wwwroot/uploads/products")}
command = ["docker", "compose", "--env-file", os.devnull, "-p", project,
           "-f", "docker-compose.yml", "-f", "docker-compose.restore.yml",
           "-f", "docker-compose.net10.yml", "config"]
model = json.loads(subprocess.check_output(command + ["--no-path-resolution", "--format", "json"], cwd=root, env=env))
services = model["services"]
require(set(services) == {"db", "web", "api"}, "Unexpected business services")
db = services["db"]
require(db["image"] == "mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd",
        "SQL image pin changed")
require(int(db["mem_limit"]) == 2304 * 1024**2 and
        str(db["environment"]["MSSQL_MEMORY_LIMIT_MB"]) == "1536", "Unexpected SQL memory limits")
require(db["stop_grace_period"] in ("1m0s", "60s", "1m"), "Unexpected SQL shutdown timeout")
mounts = {v["target"]: v for v in db["volumes"]}
data = mounts["/var/opt/mssql"]
require(data["type"] == "volume" and data["source"] == "mssql_data" and not data.get("read_only")
        and model["volumes"]["mssql_data"]["name"] == project + "_mssql_data", "Unexpected SQL data volume")
backup = mounts["/var/opt/mssql/backup"]
require(backup["type"] == "bind" and backup.get("read_only") and
        backup["source"].replace("\\", "/") == "C:/PhoneStoreDockerTransfer/backup", "Unexpected backup mount")
for service, probe in (("web", "GET / HTTP/1.1"), ("api", "GET /api/health HTTP/1.1"), ("db", "SELECT 1")):
    health = services[service]["healthcheck"]
    require(not health.get("disable") and probe in " ".join(health["test"]), f"Unexpected {service} health check")
print("Business Compose model validated (db, api, web).")
