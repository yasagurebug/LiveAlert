#!/usr/bin/env python3
import json
import re
import sys
import urllib.request

IS_LIVE_RE = re.compile(r'"isLiveNow":(true|false)')
LIVE_NOW_WINDOW = 400
LIVE_VIDEO_ID_PATTERNS = (
    re.compile(r'"videoId":"([A-Za-z0-9_-]{11})".{0,%d}?"LIVE_NOW"' % LIVE_NOW_WINDOW, re.S),
    re.compile(r'"LIVE_NOW".{0,%d}?"videoId":"([A-Za-z0-9_-]{11})"' % LIVE_NOW_WINDOW, re.S),
)
YT_INITIAL_DATA_RE = re.compile(r"var ytInitialData\s*=\s*(\{.*?\});", re.S)


def fetch(url: str, verbose: bool) -> str:
    if verbose:
        print(f"[fetch] GET {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    with urllib.request.urlopen(req, timeout=20) as resp:
        data = resp.read().decode('utf-8', errors='ignore')
        if verbose:
            print(f"[fetch] status={resp.status} bytes={len(data)}")
        return data


def extract_video_id_from_url(url: str) -> str | None:
    idx = url.lower().find("watch?v=")
    if idx < 0:
        return None
    part = url[idx + len("watch?v=") :]
    amp = part.find('&')
    if amp >= 0:
        part = part[:amp]
    return part if len(part) == 11 else None


def normalize_channel_url(url: str) -> str:
    trimmed = url.rstrip('/')
    lowered = trimmed.lower()
    for suffix in ("/streams", "/live", "/videos", "/featured"):
        if lowered.endswith(suffix):
            return trimmed[: -len(suffix)]
    return trimmed


def build_streams_url(url: str) -> str:
    base = normalize_channel_url(url)
    return base + "/streams"


def extract_live_video_id_from_initial_data(html: str) -> str | None:
    match = YT_INITIAL_DATA_RE.search(html)
    if not match:
        return None
    try:
        data = json.loads(match.group(1))
    except json.JSONDecodeError:
        return None

    live_ids: list[str] = []

    def walk(obj) -> None:
        if isinstance(obj, dict):
            video_id = obj.get("videoId")
            overlays = obj.get("thumbnailOverlays")
            if video_id and isinstance(video_id, str) and overlays:
                for overlay in overlays:
                    renderer = overlay.get("thumbnailOverlayTimeStatusRenderer")
                    if not renderer:
                        continue
                    style = renderer.get("style")
                    text = renderer.get("text") or {}
                    label = ""
                    if isinstance(text, dict):
                        if "simpleText" in text:
                            label = text.get("simpleText") or ""
                        else:
                            runs = text.get("runs") or []
                            label = "".join(r.get("text", "") for r in runs if isinstance(r, dict))
                    if style == "LIVE" or "ライブ" in label:
                        live_ids.append(video_id)
                        break
            for value in obj.values():
                walk(value)
        elif isinstance(obj, list):
            for value in obj:
                walk(value)

    walk(data)
    return live_ids[0] if live_ids else None


def find_live_video_id(html: str) -> str | None:
    live_id = extract_live_video_id_from_initial_data(html)
    if live_id:
        return live_id
    for pattern in LIVE_VIDEO_ID_PATTERNS:
        match = pattern.search(html)
        if match:
            return match.group(1)
    return None


def check_watch(video_id: str, verbose: bool) -> bool:
    html = fetch(f"https://www.youtube.com/watch?v={video_id}", verbose)
    m = IS_LIVE_RE.search(html)
    if verbose:
        print(f"[watch] isLiveNow match={m.group(1) if m else 'none'}")
    return bool(m and m.group(1).lower() == "true")


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: youtubelivedetector.py <channel_or_watch_url> [--verbose]")
        return 2
    url = sys.argv[1].strip()
    verbose = "--verbose" in sys.argv[2:] or "-v" in sys.argv[2:]
    try:
        if "watch?v=" in url.lower():
            video_id = extract_video_id_from_url(url)
            if verbose:
                print(f"[input] watch url videoId={video_id or 'none'}")
            if not video_id:
                print("NOT_LIVE")
                return 0
            print("LIVE" if check_watch(video_id, verbose) else "NOT_LIVE")
            return 0

        base_url = normalize_channel_url(url)
        streams_url = build_streams_url(url)
        if verbose:
            print(f"[input] channel url base={base_url}")
            print(f"[streams] url={streams_url}")
        streams_html = fetch(streams_url, verbose)
        video_id = find_live_video_id(streams_html)
        if verbose:
            print(f"[streams] live videoId={video_id or 'none'}")

        if not video_id:
            if verbose:
                print(f"[home] url={base_url}")
            home_html = fetch(base_url, verbose)
            video_id = find_live_video_id(home_html)
            if verbose:
                print(f"[home] live videoId={video_id or 'none'}")

        if not video_id:
            print("NOT_LIVE")
            return 0
        print("LIVE" if check_watch(video_id, verbose) else "NOT_LIVE")
        return 0
    except Exception as e:
        print(f"ERROR: {e}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
