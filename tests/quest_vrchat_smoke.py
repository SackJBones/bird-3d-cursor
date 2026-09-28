"""Normal Quest VRChat local-world deployment and private device evidence.

Uses only adb and Python's standard library. A bundle opts into replacing the
running local world; without it, only capture the current session. PNG integrity,
file transfer and launch requests are distinct from visual/physical acceptance.
"""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import time
import zlib


def png_dimensions(data):
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("Screenshot is not a PNG")
    offset, dimensions, has_pixels = 8, None, False
    while offset + 12 <= len(data):
        length = struct.unpack(">I", data[offset:offset + 4])[0]
        end = offset + 12 + length
        if end > len(data):
            raise ValueError("Truncated PNG chunk")
        kind = data[offset + 4:offset + 8]
        payload = data[offset + 8:end - 4]
        crc = struct.unpack(">I", data[end - 4:end])[0]
        if zlib.crc32(kind + payload) & 0xffffffff != crc:
            raise ValueError("PNG CRC mismatch")
        if kind == b"IHDR":
            if offset != 8 or length != 13:
                raise ValueError("Invalid PNG header")
            dimensions = struct.unpack(">II", payload[:8])
        if kind == b"IDAT":
            has_pixels = True
        if kind == b"IEND":
            if length or end != len(data) or not dimensions or not all(dimensions) or not has_pixels:
                raise ValueError("Incomplete PNG")
            return dimensions
        offset = end
    raise ValueError("Missing PNG end marker")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", required=True, type=Path)
    parser.add_argument("--serial", required=True, help="Authorized adb device serial or Wi-Fi address:port")
    parser.add_argument("--out", required=True, type=Path, help="Private, ignored evidence directory")
    parser.add_argument("--bundle", type=Path, help="Validated Android .vrcw to deploy and cold-launch")
    parser.add_argument("--world-name", help="Device filename stem, required with --bundle")
    parser.add_argument("--settle-seconds", type=float, default=15)
    args = parser.parse_args()
    if not 0 <= args.settle_seconds <= 30:
        parser.error("settle-seconds must be between 0 and 30")
    if bool(args.bundle) != bool(args.world_name):
        parser.error("bundle and world-name must be supplied together")
    if args.bundle and (not args.bundle.is_file() or args.bundle.suffix.lower() != ".vrcw" or
                        not re.fullmatch(r"[A-Za-z0-9_-]+", args.world_name)):
        parser.error("Use an existing .vrcw and a simple alphanumeric/underscore/hyphen world-name")
    args.out.mkdir(parents=True, exist_ok=True)
    # An evidence directory is an immutable observation, not an overwrite target.
    if (args.out / "device-check.json").exists():
        parser.error("Choose a new evidence directory; an observation already exists here")
    report = {"utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "capture_only": not bool(args.bundle), "visual_acceptance": "requires image inspection",
              "physical_feel_verified": False, "multiplayer_verified": False}
    base = [str(args.adb), "-s", args.serial]

    def run(command, binary=False, timeout=30):
        completed = subprocess.run(command, capture_output=True, timeout=timeout, check=True)
        return completed.stdout if binary else completed.stdout.decode("utf-8", errors="replace")

    def adb(*command, **kwargs):
        return run(base + list(command), **kwargs)

    def save(name, value):
        (args.out / name).write_text(value, encoding="utf-8")

    try:
        if re.fullmatch(r"[A-Za-z0-9.-]+:[0-9]+", args.serial):
            save("connect.txt", run([str(args.adb), "connect", args.serial]))
        if adb("get-state").strip() != "device":
            raise RuntimeError("Device is not authorized/connected")
        if args.bundle:
            remote = "/sdcard/Android/data/com.vrchat.oculus.quest/files/TestWorlds/" + args.world_name + ".vrcw"
            report["bundle_sha256"] = hashlib.sha256(args.bundle.read_bytes()).hexdigest()
            adb("shell", "mkdir", "-p", remote.rsplit("/", 1)[0])
            save("transfer.txt", adb("push", str(args.bundle), remote, timeout=60))
            report["device_sha256"] = adb("shell", "sha256sum", remote).split()[0].lower()
            report["transfer_verified"] = report["device_sha256"] == report["bundle_sha256"]
            if not report["transfer_verified"]:
                raise RuntimeError("Device bundle hash does not match source")
            adb("shell", "am", "force-stop", "com.vrchat.oculus.quest")
            launch = adb("shell", "am", "start", "-n", "com.vrchat.oculus.quest/com.unity3d.player.UnityPlayerActivity",
                         "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER",
                         "-e", "localWorldPath", remote.replace("/sdcard/", "/storage/emulated/0/"),
                         "-e", "watchWorlds", "true")
            save("launch.txt", launch)
            if re.search(r"(?im)^Error", launch):
                raise RuntimeError("Android rejected the launch request")
            report["launch_requested"] = True
            report["device_world"] = args.world_name + ".vrcw"
            time.sleep(args.settle_seconds)
        probe = subprocess.run(base + ["shell", "pidof", "com.vrchat.oculus.quest"], capture_output=True, timeout=30)
        if probe.returncode not in (0, 1):
            probe.check_returncode()
        pids = probe.stdout.decode("utf-8").split()
        if any(not pid.isdigit() for pid in pids):
            raise RuntimeError("Invalid VRChat process response")
        report["process_ids"] = pids
        report["app_process_running"] = bool(pids)
        battery = adb("shell", "dumpsys", "battery")
        save("battery.txt", battery)
        report["battery"] = dict(re.findall(r"(?m)^\s*(AC powered|USB powered|level|temperature|Weak Charger):\s*(\S+)", battery))
        if pids:
            save("memory.txt", adb("shell", "dumpsys", "meminfo", "com.vrchat.oculus.quest"))
            logs = "\n".join(adb("logcat", "-d", "--pid", pid, "-t", "3000") for pid in pids)
        else:
            # A blocked/crashed launch still needs a screen and startup evidence.
            # Keep the wider log private; never call this a successful app check.
            logs = adb("logcat", "-d", "-t", "3000")
        save("client.log", logs)
        report["matched_udon_error_lines"] = len(re.findall(
            r"(?im)^.*(?:UdonBehaviour.*exception|Udon runtime exception|An exception occurred during Udon execution|UdonBehaviour.*halted).*$", logs))
        data = adb("exec-out", "screencap", "-p", binary=True)
        report["png_dimensions"] = png_dimensions(data)
        (args.out / "headset.png").write_bytes(data)
        report["png_integrity_verified"] = True
        report["evidence_capture_complete"] = True
        if not pids:
            raise RuntimeError("No running VRChat process; inspect headset.png and private startup logs")
    except Exception as error:
        report["error"] = str(error)
        raise
    finally:
        save("device-check.json", json.dumps(report, indent=2) + "\n")
        print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
