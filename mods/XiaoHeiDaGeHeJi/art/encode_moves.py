"""Encode a labeled move reel as a looping inline GIF and, by default, an MP4."""
import argparse
import json
import subprocess
from contextlib import ExitStack
from pathlib import Path
from tempfile import TemporaryDirectory

from PIL import Image, ImageChops, ImageDraw, ImageFont


NAMES = {
    "idle": "拳击架势", "jab": "刺拳", "cross": "后手短摆拳", "uppercut": "上勾拳",
    "ready": "满层 · 双拳相碰", "slam": "杠铃片重砸", "slamHeavy": "过顶重砸",
    "charge": "借过一下", "drink": "喝一口", "aim": "瞄准队友", "throw": "抛出摇摇杯",
    "run": "奔跑", "air": "腾空", "land": "落地缓冲", "lobby": "整备", "spawn": "起身备战",
    "hit": "受击", "death": "倒地",
}
GIF_SIZE = (340, 320)


def video_writer(path, size, fps):
    import imageio_ffmpeg

    width, height = size
    return subprocess.Popen([
        imageio_ffmpeg.get_ffmpeg_exe(), "-loglevel", "error", "-y", "-f", "rawvideo",
        "-pix_fmt", "rgb24", "-s", f"{width}x{height}", "-r", str(fps), "-i", "-",
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "21", "-movflags", "+faststart",
        str(path),
    ], stdin=subprocess.PIPE)


def shared_palette(images):
    # Sample every frame. A brief muzzle flash can fall between sampled key poses.
    tile = (85, 80)
    columns = min(12, len(images))
    rows = (len(images) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * tile[0], rows * tile[1]))
    for index, frame in enumerate(images):
        position = (index % columns * tile[0], index // columns * tile[1])
        sheet.paste(frame.resize(tile, Image.Resampling.NEAREST), position)

    # Give small, luminous effects their own palette slots amid skin and studio grays.
    _, saturation, value = sheet.convert("HSV").split()
    bright_color = ImageChops.multiply(saturation.point(lambda s: 255 if s >= 28 else 0),
                                       value.point(lambda v: 255 if v >= 166 else 0))
    accents = Image.new("RGB", sheet.size)
    accents.paste(sheet, mask=bright_color)
    base = sheet.quantize(colors=224).getpalette()[:224 * 3]
    effects = accents.quantize(colors=32).getpalette()[:32 * 3]
    palette = Image.new("P", (1, 1))
    palette.putpalette(base + effects)
    return palette


def encode(source, output, stem, meta, gif_only):
    fps = meta["fps"]
    footer = meta.get("footer", "CUI GE   /   TRAINING ROOM")
    font = ImageFont.truetype("/System/Library/Fonts/STHeiti Medium.ttc", 23)
    english = ImageFont.truetype("/System/Library/Fonts/Avenir Next.ttc", 13)
    images = []
    writer = None
    with ExitStack() as stack:
        for shot in meta["shots"]:
            label = shot["label"] if "label" in shot else NAMES[shot["id"]]
            for frame in range(shot["first"], shot["last"] + 1):
                with Image.open(source / f"frame-{frame:04d}.png") as rendered:
                    image = rendered.convert("RGB")
                draw = ImageDraw.Draw(image, "RGBA")
                draw.rounded_rectangle((11, 10, 22 + draw.textlength(label, font=font), 42),
                                       radius=4, fill=(15, 23, 26, 205))
                draw.text((17, 12), label, font=font, fill=(241, 243, 237, 255))
                draw.text((14, image.height - 26), footer, font=english, fill=(195, 208, 208, 235))
                if not gif_only:
                    if writer is None:
                        writer = stack.enter_context(video_writer(output / f"{stem}.mp4", image.size, fps))
                    writer.stdin.write(image.tobytes())
                images.append(image.resize(GIF_SIZE, Image.Resampling.LANCZOS))
    if writer is not None and writer.returncode:
        raise RuntimeError("MP4 encoding failed")
    if not images:
        raise ValueError("The move reel contains no frames")

    palette = shared_palette(images)
    frames = [frame.quantize(palette=palette, dither=Image.Dither.NONE) for frame in images]
    # GIF uses centiseconds. Round absolute frame boundaries before taking deltas.
    boundaries = [round(i * 100 / fps) for i in range(len(frames) + 1)]
    durations = [10 * (end - start) for start, end in zip(boundaries, boundaries[1:])]
    frames[0].save(output / f"{stem}.gif", save_all=True, append_images=frames[1:],
                   duration=durations, loop=0, optimize=True, disposal=1)
    print(f"Encoded {len(frames)} frames at {fps} fps")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="directory containing sequence.json and frame PNGs")
    parser.add_argument("--gif-only", action="store_true", help="skip the full-resolution MP4")
    args = parser.parse_args()
    meta = json.loads((args.source / "sequence.json").read_text())
    stem = meta.get("stem", "CuiGe-Moves")
    if not stem or stem != Path(stem).name:
        raise ValueError("stem must be a filename without a directory")
    dist = Path(__file__).resolve().parents[1] / "dist"
    with TemporaryDirectory(dir=dist) as temp:
        staging = Path(temp)
        encode(args.source, staging, stem, meta, args.gif_only)
        for asset in staging.iterdir():
            asset.replace(dist / asset.name)


if __name__ == "__main__":
    main()
