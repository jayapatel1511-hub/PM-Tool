#!/usr/bin/env python3
"""Hold an authenticated live stream and report heartbeats/events.

The checker supports the local development picker and review cookie/bearer modes.
It never prints request headers or response bodies; event payloads are counted only.
"""
import argparse
import http.client
import json
import sys
import threading
import time
import urllib.parse
from queue import Empty, Queue


def connection(base, timeout):
    parsed = urllib.parse.urlsplit(base)
    cls = http.client.HTTPSConnection if parsed.scheme == "https" else http.client.HTTPConnection
    return cls(parsed.hostname, parsed.port, timeout=timeout), parsed


def run(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("base_url")
    parser.add_argument("--dev-user", help="Development picker email")
    parser.add_argument("--cookie", help="Authenticated review cookie value")
    parser.add_argument("--authorization", help="Bearer authorization value")
    parser.add_argument("--seconds", type=float, default=60)
    parser.add_argument("--heartbeat-max-gap", type=float, default=60)
    parser.add_argument("--write-path", help="Optional relative API path to POST once while the stream is held")
    parser.add_argument("--write-body", help="JSON body for --write-path")
    parser.add_argument("--no-heartbeats", action="store_true", help="Report the observed idle cutoff without requiring heartbeats")
    args = parser.parse_args(argv)
    if sum(value is not None for value in (args.dev_user, args.cookie, args.authorization)) != 1:
        parser.error("exactly one of --dev-user, --cookie or --authorization is required")
    if args.seconds <= 0 or args.heartbeat_max_gap <= 0:
        parser.error("seconds and heartbeat-max-gap must be positive")
    if args.write_path and not args.write_body:
        parser.error("--write-body is required with --write-path")
    base = args.base_url.rstrip("/")
    parsed = urllib.parse.urlsplit(base)
    if parsed.scheme not in {"http", "https"} or not parsed.hostname:
        parser.error("base_url must be an http(s) origin")
    path = "/api/v1/events"
    if args.write_path and not args.write_path.startswith("/"):
        args.write_path = "/api/v1/" + args.write_path
    started = time.monotonic()
    heartbeats = events = 0
    gaps = []
    last_data = started
    write_started = None
    first_event = None
    conn = None
    auth = {"X-Hub-Source": "UI"}
    if args.dev_user:
        auth["X-Dev-User"] = args.dev_user
    elif args.cookie:
        auth["Cookie"] = args.cookie
    else:
        auth["Authorization"] = args.authorization
    try:
        conn, _ = connection(base, 65)
        conn.request("GET", path, headers={"Accept": "text/event-stream", **auth})
        response = conn.getresponse()
        if response.status != 200:
            print(f"FAIL stream HTTP {response.status}")
            return 1
        if response.getheader("content-type", "").split(";", 1)[0].strip() != "text/event-stream":
            print("FAIL stream content type")
            return 1
        if args.write_path:
            write_started = time.monotonic()
            body = json.dumps(json.loads(args.write_body)).encode()
            writer, _ = connection(base, 15)
            writer.request("POST", args.write_path, body=body, headers={"Content-Type": "application/json", **auth})
            status = writer.getresponse().status
            writer.close()
            if status >= 400:
                print(f"FAIL write HTTP {status}")
                return 1
        # Keep the HTTP reader blocking in its own daemon thread. Reading the buffered
        # response conditionally after poking the raw socket poisons http.client's
        # buffer on ordinary heartbeat gaps (and is especially fragile over TLS).
        lines = Queue()
        def read_lines():
            try:
                while True:
                    value = response.readline()
                    lines.put((time.monotonic(), value))
                    if not value:
                        return
            except OSError as exc:
                lines.put((time.monotonic(), exc))
        threading.Thread(target=read_lines, daemon=True).start()
        while time.monotonic() - started < args.seconds:
            observation_left = args.seconds - (time.monotonic() - started)
            if observation_left <= 0:
                break
            wait_for = min(args.heartbeat_max_gap - (time.monotonic() - last_data), observation_left)
            try:
                observed_at, line = lines.get(timeout=max(0.01, wait_for))
            except Empty:
                if time.monotonic() - started >= args.seconds:
                    break
                gap = time.monotonic() - last_data
                if args.no_heartbeats:
                    # An observation timeout is not evidence that the server closed the
                    # connection. Keep observing; only an actual EOF proves an idle cutoff.
                    continue
                print(f"FAIL heartbeat gap {gap:.1f}s")
                return 1
            if isinstance(line, OSError):
                if args.no_heartbeats:
                    print(json.dumps({"result": "UNPROVEN", "seconds": round(observed_at - started, 3),
                                      "heartbeats": heartbeats, "events": events, "heartbeatCheck": False,
                                      "reason": "stream read failed before EOF"}))
                    return 1
                print("FAIL stream read")
                return 1
            if not line:
                if args.no_heartbeats:
                    print(json.dumps({"result": "PASS", "seconds": round(time.monotonic() - started, 3),
                                      "heartbeats": heartbeats, "events": events, "idleCutoffSeconds": round(time.monotonic() - last_data, 3),
                                      "heartbeatCheck": False}))
                    return 0
                print("FAIL stream disconnected")
                return 1
            now = observed_at
            gaps.append(now - last_data)
            last_data = now
            if line.startswith(b":"):
                heartbeats += 1
            elif line.startswith(b"data:"):
                events += 1
                if first_event is None:
                    first_event = now
            if write_started is not None and first_event is not None and first_event - write_started > 2:
                print(f"FAIL event latency {first_event - write_started:.3f}s")
                return 1
            if write_started is not None and first_event is not None:
                # A write-triggered event has arrived within the required two-second bound.
                write_started = None
        if args.write_path and first_event is None:
            print("FAIL write produced no live event")
            return 1
        if args.no_heartbeats:
            print(json.dumps({"result": "UNPROVEN", "seconds": round(time.monotonic() - started, 3),
                              "heartbeats": heartbeats, "events": events, "heartbeatCheck": False,
                              "reason": "observation ended without EOF/disconnect"}))
            return 1
        if heartbeats == 0:
            # Silence is not proof of a live stream; the server opens every stream with a comment line.
            print("FAIL no heartbeat observed")
            return 1
        maximum = max(gaps, default=0)
        print(json.dumps({"result": "PASS", "seconds": args.seconds, "heartbeats": heartbeats, "events": events,
                          "maxGapSeconds": round(maximum, 3), "heartbeatCheck": not args.no_heartbeats}))
        return 0
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"FAIL stream: {type(exc).__name__}")
        return 1
    finally:
        if conn is not None:
            conn.close()


if __name__ == "__main__":
    sys.exit(run())
