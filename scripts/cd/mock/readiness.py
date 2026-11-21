import json
import os
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.error import HTTPError
from urllib.request import urlopen


class Readiness(BaseHTTPRequestHandler):
    def do_GET(self):
        version = os.environ["MOCK_VERSION"]
        if self.path not in ("/", "/api/health/ready", "/version"):
            self.send_error(404)
            return
        status = 503 if version == "unready" and self.path != "/version" else 200
        body = json.dumps({"version": version, "ready": status == 200}).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


if len(sys.argv) == 3 and sys.argv[1] == "--probe":
    try:
        with urlopen(f"http://127.0.0.1:8080{sys.argv[2]}", timeout=1) as response:
            print(json.dumps({"status": response.status, **json.load(response)}))
    except HTTPError as error:
        print(json.dumps({"status": error.code}))
else:
    HTTPServer(("0.0.0.0", 8080), Readiness).serve_forever()
