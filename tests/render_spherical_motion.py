"""Encode the Unity fixture's 120-frame sequence; requires Pillow, no Unity dependency."""
import argparse
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument("captures", type=Path, help="The generated MenuCaptures directory")
args = parser.parse_args()
paths = sorted((args.captures / "SphereMotionFrames").glob("frame-*.png"))
if len(paths) != 120:
    raise SystemExit(f"Expected 120 rendered frames, found {len(paths)}")
frames = []
for path in paths:
    with Image.open(path) as source:
        frames.append(source.resize((640, 450), Image.Resampling.LANCZOS).quantize(colors=192))
output = args.captures / "spherical-scroll-motion.gif"
# GIF uses centiseconds: 30/30/40 ms preserves the 30 Hz sequence's total duration.
frames[0].save(output, save_all=True, append_images=frames[1:], duration=[30, 30, 40] * 40,
               loop=0, disposal=2, optimize=False)
with Image.open(output) as result:
    assert result.n_frames == 120
    duration = 0
    for index in range(result.n_frames):
        result.seek(index)
        duration += result.info["duration"]
    assert duration == 4000
print(f"{output}: 120 frames, {duration} ms, {output.stat().st_size} bytes")
