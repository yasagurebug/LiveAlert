#!/usr/bin/env python3
import json
import re
import sys
import urllib.request

IS_LIVE_RE = re.compile(r'"isLiveNow":(true|false)')
VIDEO_ID_IN_STRING_RE = re.compile(r'(?:/vi/|[?&]v=)([A-Za-z0-9_-]{11})')
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


def is_video_id(value: str) -> bool:
    return len(value) == 11 and all(
        "A" <= c <= "Z" or "a" <= c <= "z" or "0" <= c <= "9" or c in "_-"
        for c in value
    )


def extract_video_id_from_string(value: str | None) -> str | None:
    if not value:
        return None
    if is_video_id(value):
        return value
    match = VIDEO_ID_IN_STRING_RE.search(value)
    return match.group(1) if match else None


def is_live_thumbnail_badge(obj: dict) -> bool:
    style = obj.get("badgeStyle")
    if isinstance(style, str) and style.lower() == "thumbnail_overlay_badge_style_live":
        return True

    text = obj.get("text")
    return isinstance(text, str) and "ライブ" in text


def has_live_badge(obj) -> bool:
    if isinstance(obj, dict):
        badge = obj.get("thumbnailBadgeViewModel")
        if isinstance(badge, dict) and is_live_thumbnail_badge(badge):
            return True
        return any(has_live_badge(value) for value in obj.values())
    if isinstance(obj, list):
        return any(has_live_badge(value) for value in obj)
    return False


def find_video_id_reference(obj) -> str | None:
    if isinstance(obj, dict):
        for value in obj.values():
            if isinstance(value, str):
                video_id = extract_video_id_from_string(value)
                if video_id:
                    return video_id
        for value in obj.values():
            video_id = find_video_id_reference(value)
            if video_id:
                return video_id
    elif isinstance(obj, list):
        for value in obj:
            video_id = find_video_id_reference(value)
            if video_id:
                return video_id
    elif isinstance(obj, str):
        return extract_video_id_from_string(obj)
    return None


def is_likely_video_item(obj: dict) -> bool:
    return any(
        key in obj
        for key in (
            "richItemRenderer",
            "lockupViewModel",
            "videoRenderer",
            "gridVideoRenderer",
            "compactVideoRenderer",
        )
    )


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
                    if (isinstance(style, str) and style.lower() == "live") or "ライブ" in label:
                        live_ids.append(video_id)
                        break
            if is_likely_video_item(obj) and has_live_badge(obj):
                video_id = find_video_id_reference(obj)
                if video_id:
                    live_ids.append(video_id)
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
