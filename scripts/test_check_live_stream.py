import importlib.util
import io
import pathlib
import threading
import unittest
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from contextlib import redirect_stdout


ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("check_live_stream", ROOT / "scripts/check-live-stream.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class LiveStreamCheckerTests(unittest.TestCase):
    class _SseHandler(BaseHTTPRequestHandler):
        body = b": heartbeat\n\n"
        close_after = False
        echo_writes = False

        def do_GET(self):
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Cache-Control", "no-cache")
            self.end_headers()
            self.wfile.write(self.body)
            self.wfile.flush()
            if self.close_after:
                self.connection.shutdown(1)
                return
            if self.echo_writes and self.server.wrote.wait(2):
                self.wfile.write(b"data: {}\n\n")
                self.wfile.flush()
            threading.Event().wait(2)

        def do_POST(self):
            self.rfile.read(int(self.headers["Content-Length"]))
            self.send_response(204)
            self.end_headers()
            if self.echo_writes:
                self.server.wrote.set()

        def log_message(self, *_args):
            pass

    def _serve(self, close_after=False, **behaviour):
        handler = type("Handler", (self._SseHandler,), {"close_after": close_after, **behaviour})
        server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
        server.wrote = threading.Event()
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        return server, thread

    def test_rejects_write_without_body(self):
        with self.assertRaises(SystemExit):
            MODULE.run(["http://127.0.0.1:1", "--dev-user", "alex@hub.test", "--write-path", "tasks"])

    def test_refuses_non_stream_response_without_printing_headers(self):
        output = io.StringIO()
        with redirect_stdout(output):
            result = MODULE.run(["http://127.0.0.1:1", "--dev-user", "alex@hub.test", "--seconds", "1"])
        self.assertEqual(result, 1)
        self.assertNotIn("X-Dev-User", output.getvalue())

    def _check(self, server, *extra):
        output = io.StringIO()
        with redirect_stdout(output):
            result = MODULE.run([f"http://127.0.0.1:{server.server_port}", "--dev-user", "alex@hub.test", *extra])
        return result, output.getvalue()

    def test_local_stream_heartbeat_passes(self):
        # A one-second look at a stream whose gap limit is two seconds ends without a gap failure.
        server, thread = self._serve()
        try:
            result, output = self._check(server, "--seconds", "1", "--heartbeat-max-gap", "2")
            self.assertEqual(result, 0)
            self.assertIn('"result": "PASS"', output)
            self.assertIn('"heartbeats": 1', output)
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)

    def test_silent_stream_fails(self):
        server, thread = self._serve(body=b"")
        try:
            result, output = self._check(server, "--seconds", "1", "--heartbeat-max-gap", "2")
            self.assertEqual(result, 1)
            self.assertIn("FAIL no heartbeat observed", output)
            result, output = self._check(server, "--seconds", "1", "--heartbeat-max-gap", ".3")
            self.assertEqual(result, 1)
            self.assertIn("FAIL heartbeat gap", output)
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)

    def test_write_must_produce_a_live_event(self):
        write = ["--seconds", "1", "--heartbeat-max-gap", "2", "--write-path", "tasks", "--write-body", "{}"]
        server, thread = self._serve(echo_writes=True)
        try:
            result, output = self._check(server, *write)
            self.assertEqual(result, 0, output)
            self.assertIn('"events": 1', output)
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)
        server, thread = self._serve()
        try:
            result, output = self._check(server, *write)
            self.assertEqual(result, 1)
            self.assertIn("FAIL write produced no live event", output)
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)

    def test_no_heartbeat_requires_real_eof(self):
        server, thread = self._serve()
        try:
            output = io.StringIO()
            with redirect_stdout(output):
                result = MODULE.run([f"http://127.0.0.1:{server.server_port}", "--dev-user", "alex@hub.test", "--seconds", ".2", "--no-heartbeats"])
            self.assertEqual(result, 1)
            self.assertIn('"result": "UNPROVEN"', output.getvalue())
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)

    def test_no_heartbeat_reports_cutoff_only_after_eof(self):
        server, thread = self._serve(close_after=True)
        try:
            output = io.StringIO()
            with redirect_stdout(output):
                result = MODULE.run([f"http://127.0.0.1:{server.server_port}", "--dev-user", "alex@hub.test", "--seconds", "1", "--no-heartbeats"])
            self.assertEqual(result, 0)
            self.assertIn('"result": "PASS"', output.getvalue())
            self.assertIn('"idleCutoffSeconds"', output.getvalue())
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)


if __name__ == "__main__":
    unittest.main()
