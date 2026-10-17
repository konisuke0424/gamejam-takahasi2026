"""Concept images of the monster for Tripo image-to-3D (Gemini image model on Vertex AI).

Needs Google Cloud application-default credentials (gcloud auth application-default login) and
GOOGLE_CLOUD_PROJECT / GOOGLE_CLOUD_LOCATION, either in the environment or in a .env file given
with --dotenv (only those two keys are read from it).

  python Tools/concept/gen_monster_concept.py --dotenv path/to/.env [--set base|noh] [--count N]

Writes Art/Concept/monster_concept_NN.png (set "base") or monster_noh_NN.png (set "noh": one per
Noh mask type).
"""
import argparse
import concurrent.futures as cf
import os
import time
from pathlib import Path

from google import genai
from google.genai import types

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "Art" / "Concept"
MODEL = "gemini-3-pro-image"

# What Tripo needs to rebuild a clean, riggable mesh from one picture.
TRIPO_RULES = (
    "Character turnaround reference for 3D modeling: ONE character only, full body from the top of the head "
    "to the soles of the feet, front view facing the camera straight on, camera at chest height, "
    "neutral A-pose (arms held slightly away from the body at about 30 degrees, hands open, palms down), "
    "legs slightly apart, plain seamless pure white background, soft even studio lighting from the front, "
    "no cast shadows, no floor, no props, no text, the whole figure centered and fully inside the frame, "
    "photorealistic, highly detailed realistic materials and skin, sharp focus."
)

BODY = (
    "A Japanese folk-horror monster that imitates a mother's voice: an onryo-like apparition about 2.5 metres "
    "tall, unnaturally tall and emaciated, slightly hunched. Very long straight wet-looking black hair falling "
    "past the waist. It wears a tattered, dirty black kimono with wide ragged sleeves, the hem ending at mid-shin "
    "so its thin grey bare feet and ankles are visible. Its arms are far too long and thin, pale grey skin, with "
    "long bony fingers reaching below the knees."
)

FACE = (
    "A few strands of hair hang over its face. Its face is a pale porcelain, Noh-mask-like face, too long and "
    "smooth, with narrow upturned hollow black eye sockets each holding a tiny pinpoint of light, black tear-like "
    "streaks running down from the eyes, and a long vertically gaping black mouth."
)

NOH = (
    "Its face is completely hidden behind a real, traditional carved wooden Japanese Noh mask worn over the face, "
    "tied on with cords behind the head, the long black hair falling around the edges of the mask; old cracked "
    "lacquer, aged and stained. The mask is {mask} Nothing of the real face is visible, only darkness inside "
    "the small eye holes of the mask."
)

# Variant sets: (file prefix, list of (design text, variant text)).
SETS = {
    "base": ("monster_concept", [
        (BODY + " " + FACE, "Expression: blank and still, mouth gaping wide."),
        (BODY + " " + FACE, "Expression: head tilted unnaturally to one side, mouth gaping."),
        (BODY + " " + FACE, "Kimono more torn and frayed, hair more dishevelled."),
        (BODY + " " + FACE, "Skin slightly more cadaverous, kimono faded charcoal black."),
    ]),
    "noh": ("monster_noh", [
        (BODY + " " + NOH.format(mask="a Ko-omote mask: a young woman's face, white skin, faint serene smile, "
                                       "painted high eyebrows, blackened teeth just visible."), ""),
        (BODY + " " + NOH.format(mask="a Fukai mask: a middle-aged mother's face, sorrowful, with slightly sunken "
                                       "cheeks and faint lines, ivory skin."), ""),
        (BODY + " " + NOH.format(mask="a Yase-onna mask: the emaciated face of a dead woman's ghost, hollow cheeks, "
                                       "grey-white, exhausted haunted expression."), ""),
        (BODY + " " + NOH.format(mask="a Deigan mask: a woman's face whose eyes are gilded with gold paint, "
                                       "faintly glowing gold eyes, eerie jealous expression."), ""),
        (BODY + " " + NOH.format(mask="a Hannya mask: a jealous demon woman with two sharp horns, bulging gilded "
                                       "eyes and a wide grinning mouth with fangs."), ""),
        (BODY + " " + NOH.format(mask="a Zo-onna mask: a dignified, cold, divine woman's face, perfectly calm, "
                                       "with deep black holes for eyes."), ""),
    ]),
}


def read_dotenv(path):
    """Copy only the project/location keys from a .env into the environment."""
    if not path:
        return
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if "=" not in line or line.lstrip().startswith("#"):
            continue
        k, v = line.split("=", 1)
        k = k.strip()
        if k in ("GOOGLE_CLOUD_PROJECT", "GOOGLE_CLOUD_LOCATION") and k not in os.environ:
            os.environ[k] = v.strip().strip('"').strip("'")


def generate(client, prefix, index, design, variant):
    prompt = f"{design} {variant} {TRIPO_RULES}"
    config = types.GenerateContentConfig(response_modalities=["IMAGE", "TEXT"])
    try:
        config.image_config = types.ImageConfig(aspect_ratio="2:3")
    except Exception:
        pass
    for attempt in range(4):
        try:
            resp = client.models.generate_content(model=MODEL, contents=[prompt], config=config)
            for cand in resp.candidates or []:
                for part in (cand.content.parts if cand.content else []) or []:
                    if getattr(part, "inline_data", None) and part.inline_data.data:
                        out = OUT / f"{prefix}_{index:02d}.png"
                        out.write_bytes(part.inline_data.data)
                        return f"ok {out.name}"
            raise RuntimeError("no image in response")
        except Exception as e:
            err = e
            time.sleep(4 * (2 ** attempt))
    return f"error {index}: {type(err).__name__}: {err}"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dotenv")
    ap.add_argument("--set", choices=list(SETS), default="base")
    ap.add_argument("--count", type=int, default=0, help="0 = one image per variant in the set")
    ap.add_argument("--only", default="", help="comma-separated image numbers to (re)generate, e.g. 3,4")
    ap.add_argument("--workers", type=int, default=2, help="parallel requests (higher hits the per-minute quota)")
    # The Gemini 3 image models answer on the global endpoint only (us-central1 returns 404).
    ap.add_argument("--location", default="global")
    args = ap.parse_args()
    read_dotenv(args.dotenv)
    OUT.mkdir(parents=True, exist_ok=True)
    client = genai.Client(vertexai=True, project=os.environ["GOOGLE_CLOUD_PROJECT"], location=args.location)
    prefix, variants = SETS[args.set]
    count = args.count or len(variants)
    jobs = [(prefix, i + 1, *variants[i % len(variants)]) for i in range(count)]
    if args.only:
        wanted = {int(x) for x in args.only.split(",")}
        jobs = [j for j in jobs if j[1] in wanted]
    with cf.ThreadPoolExecutor(max_workers=max(1, args.workers)) as ex:
        for line in ex.map(lambda j: generate(client, *j), jobs):
            print(line, flush=True)


if __name__ == "__main__":
    main()
