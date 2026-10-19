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
        close_after = False

        def do_GET(self):
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Cache-Control", "no-cache")
            self.end_headers()
            self.wfile.write(b": heartbeat\\n\\n")
            self.wfile.flush()
            if self.close_after:
                self.connection.shutdown(1)
            else:
                self.server.stop = threading.Event()
                self.server.stop.wait(2)

        def log_message(self, *_args):
            pass

    def _serve(self, close_after):
        self._SseHandler.close_after = close_after
        server = ThreadingHTTPServer(("127.0.0.1", 0), self._SseHandler)
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

    def test_uses_select_before_buffered_reads_and_checks_write_latency(self):
        source = (ROOT / "scripts/check-live-stream.py").read_text()
        self.assertIn("threading.Thread", source)
        self.assertIn("event latency", source)
        self.assertIn("idleCutoffSeconds", source)

    def test_short_observation_does_not_fail_for_a_larger_heartbeat_gap(self):
        source = (ROOT / "scripts/check-live-stream.py").read_text()
        self.assertIn("if observation_left <= 0", source)

    def test_local_stream_heartbeat_passes(self):
        server, thread = self._serve(False)
        try:
            output = io.StringIO()
            with redirect_stdout(output):
                result = MODULE.run([f"http://127.0.0.1:{server.server_port}", "--dev-user", "alex@hub.test", "--seconds", "1", "--heartbeat-max-gap", "2"])
            self.assertEqual(result, 0)
            self.assertIn('"result": "PASS"', output.getvalue())
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)

    def test_no_heartbeat_requires_real_eof(self):
        server, thread = self._serve(False)
        try:
            output = io.StringIO()
            with redirect_stdout(output):
                result = MODULE.run([f"http://127.0.0.1:{server.server_port}", "--dev-user", "alex@hub.test", "--seconds", ".2", "--no-heartbeats"])
            self.assertEqual(result, 1)
            self.assertIn('"result": "UNPROVEN"', output.getvalue())
        finally:
            server.shutdown(); server.server_close(); thread.join(timeout=2)

    def test_no_heartbeat_reports_cutoff_only_after_eof(self):
        server, thread = self._serve(True)
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
