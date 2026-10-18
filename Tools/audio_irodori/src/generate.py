"""voice_script.json の全セリフを Irodori-TTS で生成する（各 ID につき near+far の2ファイル）。日本語コメント付き。"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys
import zlib

ROOT = Path(__file__).resolve().parent.parent

_CFG_KEYS = (
    ("cfg_scale_text", "--cfg-scale-text", 3.0),
    ("cfg_scale_caption", "--cfg-scale-caption", 3.0),
    ("cfg_scale_speaker", "--cfg-scale-speaker", 5.0),
)


def _cfg_value(defaults: dict, irodori: dict, character: str, key: str, fallback: float) -> float:
    """たかしは defaults の takashi_cfg_scale_* を既定に使える。"""
    if key in irodori:
        return float(irodori[key])
    if character == "takashi":
        tkey = "takashi_" + key
        if tkey in defaults:
            return float(defaults[tkey])
    return float(defaults.get(key, fallback))


def infer_cfg_argv(defaults: dict, irodori: dict, character: str) -> list[str]:
    """infer.py の CFG 引数。voice の irodori が defaults を上書き。"""
    out: list[str] = []
    for key, flag, fallback in _CFG_KEYS:
        val = _cfg_value(defaults, irodori, character, key, fallback)
        out.extend([flag, str(val)])
    return out


def duration_scale_for(defaults: dict, irodori: dict, character: str) -> float:
    """たかしは予測尺が長めになり末尾幻覚が出やすいので既定を短くできる。"""
    if character == "takashi":
        return float(defaults.get("takashi_duration_scale", irodori.get("duration_scale", 1.0)))
    return float(irodori.get("duration_scale", 1.0))


def infer_caption(defaults: dict, irodori: dict, character: str) -> str | None:
    """infer に渡す caption。None なら --caption を付けない（テキスト条件を優先）。"""
    if irodori.get("use_caption") is False:
        return None
    if character == "takashi" and defaults.get("takashi_infer_use_caption") is False:
        return None
    cap = irodori.get("caption")
    if cap is None or not str(cap).strip():
        return None
    return str(cap)


def infer_argv_tail(
    defaults: dict,
    irodori: dict,
    character: str,
    text: str,
    reference: Path,
    rawpath: Path,
    seed: int,
    device: str,
) -> list[str]:
    """infer.py へ渡す引数列（uv run より後ろ）。caption は任意。"""
    argv: list[str] = [
        "--hf-checkpoint",
        defaults["model"],
        "--text",
        text,
    ]
    cap = infer_caption(defaults, irodori, character)
    if cap is not None:
        argv.extend(["--caption", cap])
    argv.extend(
        [
            "--ref-wav",
            str(reference),
            "--duration-scale",
            str(duration_scale_for(defaults, irodori, character)),
            *infer_cfg_argv(defaults, irodori, character),
            "--seed",
            str(seed),
            "--num-steps",
            str(defaults.get("num_steps", 40)),
            "--model-device",
            device,
            "--codec-device",
            device,
            "--output-wav",
            str(rawpath),
        ]
    )
    return argv


def validate(config):
    """ファイル名・加工値などを事前検証。生成前に不正な設定を弾く。"""
    ids=set()
    for v in config['voices']:
        ident=v['id']
        if not re.fullmatch(r'(mother|monster|chase|catch|takashi)_\d{2}',ident):
            raise ValueError(f'ID形式が不正: {ident}')
        if ident in ids: raise ValueError(f'ID重複: {ident}')
        ids.add(ident)
        if v['filename']!=ident+'.wav':raise ValueError(f'{ident}: 出力名が不一致')
        n=v['noise']['level']
        if type(n)!=int or not 0<=n<=9 or v['noise']['enabled']!=(n>0):
            raise ValueError(f'{ident}: noise設定が不正')
        # 妖怪用エフェクトの形式を検証。依存ライブラリは音声生成時に読み込む。
        from effects import validate_fx
        validate_fx(v.get('fx',{}))
        if n != 0: raise ValueError(f'{ident}: V7ではノイズを使いません')
    return config['voices']


def main():
    ap=argparse.ArgumentParser(description='Irodori-TTS 音声素材一括生成')
    ap.add_argument('--only',help='mother_02、monster_01など1本だけ生成')
    ap.add_argument('--dry-run',action='store_true',help='音声生成せずJSONのみ確認')
    ap.add_argument('--repo',help='Irodori-TTSのインストール先を上書き')
    ap.add_argument('--device',choices=['cpu','cuda'],help='CPU/GPUを選択（既定cpu）')
    ap.add_argument('--fx-only',action='store_true',help='既存raw WAVを再加工する。Irodori実行なし')
    ap.add_argument('--skip-existing',action='store_true',help='生成済みのexportを再生成しない')
    args=ap.parse_args()
    config=json.loads((ROOT/'config/voice_script.json').read_text(encoding='utf-8'))
    voices=validate(config)
    if args.only:
        voices=[v for v in voices if v['id']==args.only]
        if not voices:ap.error(f'IDが見つかりません: {args.only}')
    for v in voices: print(f"{v['id']}: {v['text']} / noise={v['noise']['level']} / fx={v.get('fx',{})}")
    defaults_preview = config["irodori_defaults"]
    if args.dry_run:
        for v in voices:
            irod = v["irodori"]
            cap = infer_caption(defaults_preview, irod, v.get("character", ""))
            cfg = infer_cfg_argv(defaults_preview, irod, v.get("character", ""))
            print(
                f"  infer: caption={'なし' if cap is None else 'あり'}  "
                + " ".join(f"{cfg[i]}={cfg[i+1]}" for i in range(0, len(cfg), 2))
            )
        print(f"設定検証OK: {len(voices)}本（音声未生成）")
        return
    # 音声加工の追加ライブラリは生成するときだけ読み込む。
    from effects import process
    from game_voices import prefar_path, write_near_far_pair
    from speech_trim import trim_trailing_hallucination_file
    defaults=config['irodori_defaults']
    repo=Path(args.repo or defaults['repo_path'])
    if not args.fx_only and not (repo/'infer.py').is_file():raise FileNotFoundError(f'Irodori-TTSのinfer.pyがありません: {repo}')
    def reference_for(voice):
        """母・妖怪は mother.wav。たかしは takashi_reference（未設定時は mother と同じパス）。"""
        if voice.get('character') == 'takashi':
            rel = voice.get('reference') or defaults.get('takashi_reference') or defaults['reference']
        else:
            rel = defaults['reference']
        return ROOT / rel
    raw=ROOT/'assets/raw'; export=ROOT/'assets/export'
    raw.mkdir(parents=True,exist_ok=True);export.mkdir(parents=True,exist_ok=True)
    device=args.device or defaults.get('device','cpu')
    voice_index = {v['id']: i for i, v in enumerate(config['voices'])}
    for v in voices:
        i = voice_index[v['id']]
        rawpath=raw/v['filename'];outpath=export/v['filename']
        farpath=outpath.with_name(outpath.stem+'_far.wav')
        if args.skip_existing and outpath.exists() and farpath.exists():
            print('スキップ:',v['id']);continue
        reference = reference_for(v)
        if not reference.is_file():
            raise FileNotFoundError(f'参照音声がありません: {reference} （{v["id"]}）')
        # Irodori本体に含まれるuv環境を利用。別環境のPythonを混ぜない。
        if args.fx_only:
            if not rawpath.is_file(): raise FileNotFoundError(f'加工する元WAVがありません: {rawpath}')
            if v.get("character") == "takashi" and defaults.get("takashi_trim_trailing_speech", True):
                if trim_trailing_hallucination_file(rawpath):
                    print("末尾トリム:", v["id"], flush=True)
            fx_out = prefar_path(outpath)
            process(rawpath, fx_out, 0, v.get('fx', {}), zlib.crc32(v['id'].encode()))
            # 距離用 near/far は prefar から作る（near 済み WAV を二重加工しない）
            write_near_far_pair(fx_out, outpath, v['character'], i)
            print('再加工:', outpath, flush=True)
            continue
        irod = v["irodori"]
        if irod.get("seed") is not None:
            seed = int(irod["seed"]) % 2147483647
        else:
            seed = (int(defaults["seed"]) + zlib.crc32(v["id"].encode())) % 2147483647
        command = [
            "uv",
            "run",
            "--no-sync",
            "python",
            "infer.py",
            *infer_argv_tail(defaults, irod, v.get("character", ""), v["text"], reference, rawpath, seed, device),
        ]
        cap = infer_caption(defaults, irod, v.get("character", ""))
        print(
            "生成:",
            v["id"],
            f"caption={'off' if cap is None else 'on'}",
            " ".join(infer_cfg_argv(defaults, irod, v.get("character", ""))),
            flush=True,
        )
        # shell=Falseを維持し、セリフに記号が含まれても安全に引数を渡す。
        subprocess.run(command,cwd=repo,check=True)
        if v.get("character") == "takashi" and defaults.get("takashi_trim_trailing_speech", True):
            if trim_trailing_hallucination_file(rawpath):
                print("末尾トリム:", v["id"], flush=True)
        # 元音と加工後を別保存。生成後の声にノイズを入れず補助FXだけ足す。
        fx_out = prefar_path(outpath)
        process(rawpath, fx_out, v['noise']['level'], v.get('fx', {}), zlib.crc32(v['id'].encode()))
        # prefar を距離用 near/far に展開（ゲームは build_audio を使わない）
        write_near_far_pair(fx_out, outpath, v['character'], i)
        print('保存:', outpath, flush=True)

if __name__=='__main__':main()
