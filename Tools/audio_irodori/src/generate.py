"""既存JSONを使いIrodori-TTSで22セリフを生成する（export は near+far の44ファイル）。日本語コメント付き。"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import sys
import zlib

ROOT = Path(__file__).resolve().parent.parent


def validate(config):
    """ファイル名・加工値などを事前検証。生成前に不正な設定を弾く。"""
    ids=set()
    for v in config['voices']:
        ident=v['id']
        if not re.fullmatch(r'(mother|monster|chase|catch)_\d{2}',ident):
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
    if args.dry_run:
        print(f'設定検証OK: {len(voices)}本（音声未生成）')
        return
    # 音声加工の追加ライブラリは生成するときだけ読み込む。
    from effects import process
    from game_voices import prefar_path, write_near_far_pair
    defaults=config['irodori_defaults']
    repo=Path(args.repo or defaults['repo_path'])
    if not args.fx_only and not (repo/'infer.py').is_file():raise FileNotFoundError(f'Irodori-TTSのinfer.pyがありません: {repo}')
    reference=ROOT/defaults['reference']
    if not reference.is_file():raise FileNotFoundError(f'参照音声がありません: {reference}')
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
        # Irodori本体に含まれるuv環境を利用。別環境のPythonを混ぜない。
        if args.fx_only:
            if not rawpath.is_file(): raise FileNotFoundError(f'加工する元WAVがありません: {rawpath}')
            fx_out = prefar_path(outpath)
            process(rawpath, fx_out, 0, v.get('fx', {}), zlib.crc32(v['id'].encode()))
            # 距離用 near/far は prefar から作る（near 済み WAV を二重加工しない）
            write_near_far_pair(fx_out, outpath, v['character'], i)
            print('再加工:', outpath, flush=True)
            continue
        command=['uv','run','--no-sync','python','infer.py',
                 '--hf-checkpoint',defaults['model'],
                 '--text',v['text'],
                 '--caption',v['irodori']['caption'],
                 '--ref-wav',str(reference),
                 '--duration-scale',str(v['irodori'].get('duration_scale',1)),
                 '--seed',str((int(defaults['seed'])+zlib.crc32(v['id'].encode()))%2147483647),
                 '--num-steps',str(defaults.get('num_steps',40)),
                 '--model-device',device,'--codec-device',device,
                 '--output-wav',str(rawpath)]
        print('生成:',v['id'],flush=True)
        # shell=Falseを維持し、セリフに記号が含まれても安全に引数を渡す。
        subprocess.run(command,cwd=repo,check=True)
        # 元音と加工後を別保存。生成後の声にノイズを入れず補助FXだけ足す。
        fx_out = prefar_path(outpath)
        process(rawpath, fx_out, v['noise']['level'], v.get('fx', {}), zlib.crc32(v['id'].encode()))
        # prefar を距離用 near/far に展開（ゲームは build_audio を使わない）
        write_near_far_pair(fx_out, outpath, v['character'], i)
        print('保存:', outpath, flush=True)

if __name__=='__main__':main()
