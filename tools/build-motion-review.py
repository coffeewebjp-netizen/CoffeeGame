"""Encode dense development-player captures using their measured frame times."""
import argparse
import json
import math
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture_directory", type=Path)
    args = parser.parse_args()
    directory = args.capture_directory.resolve()
    report = json.loads((directory / "motion-progress.json").read_text(encoding="utf-8-sig"))
    if not report.get("denseVideoFrames"):
        raise ValueError("Capture with -captureMeshyMotionVideo; sparse checkpoints are not a motion video.")
    encoder = shutil.which("ffmpeg")
    if not encoder:
        raise FileNotFoundError("ffmpeg must be on PATH")
    sequences = ["run", "jump-ascent", "jump-fall", "jump-land", "sword", "magic-charge", "magic-release"]
    if any(sample['sequence'] == 'dodge' for sample in report['samples']):
        sequences.append('dodge')
    lines = ["ffconcat version 1.0"]
    timeline = []
    total = 0.0
    last_file = None
    for sequence in sequences:
        samples = [sample for sample in report["samples"] if sample["sequence"] == sequence]
        if len(samples) < 5:
            raise ValueError(f"Missing dense sequence: {sequence}")
        start = total
        for index, sample in enumerate(samples):
            frame = (directory / sample["image"]).resolve()
            if frame.parent != directory or frame.suffix.lower() != ".png" or not frame.is_file():
                raise ValueError(f"Invalid capture path: {frame}")
            duration = samples[index + 1]["actualSeconds"] - sample["actualSeconds"] if index + 1 < len(samples) else 1 / 30
            if not math.isfinite(duration) or duration <= 0:
                raise ValueError(f"Invalid measured timing in {sequence}")
            last_file = "file '" + frame.as_posix().replace("'", "'\\''") + "'"
            lines += [last_file, f"duration {duration:.9f}"]
            total += duration
        timeline.append({"sequence": sequence, "videoStartSeconds": start, "durationSeconds": total - start,
                         "frames": len(samples), "source": "measured development-player presentation capture"})
    lines.append(last_file)
    concat = directory / "motion-review.ffconcat"
    concat.write_text("\n".join(lines) + "\n", encoding="utf-8")
    output = directory / "motion-review.mp4"
    subprocess.run([encoder, "-n", "-hide_banner", "-loglevel", "warning", "-safe", "0", "-f", "concat",
                    "-i", str(concat), "-fps_mode", "vfr", "-an", "-c:v", "libx264", "-crf", "18",
                    "-pix_fmt", "yuv420p", "-movflags", "+faststart", str(output)], check=True)
    (directory / "motion-review-timeline.json").write_text(json.dumps(timeline, indent=2) + "\n", encoding="utf-8")
    print(output)


if __name__ == "__main__":
    main()
