"""Irodori-TTS生成後のWAVに、原音を残した妖怪エフェクトを加える。

母親の音声は加工しない。妖怪は同じ参照音声を利用し、声色を壊さない軽い追加演出を行う。
ホワイトノイズやヒスノイズの付加は行わない。定位・左右パンも音源には焼き込まない。
"""
import numpy as np
import soundfile as sf


def _pitch_shift(audio, sr, semitones):
    """声の高さだけを半音単位で変更する。速度はなるべく維持する。"""
    import librosa
    return np.stack([librosa.effects.pitch_shift(audio[:, ch], sr=sr, n_steps=semitones)
                     for ch in range(audio.shape[1])], axis=1).astype(np.float32)


def _stutter(audio, sr, at):
    """指定した相対位置(0〜1)の手前の音を繰り返す。V6の効果を互換維持。"""
    at = float(at)
    pos = int(np.clip(at, 0, 1) * len(audio))
    size = min(int(.10 * sr), pos)
    if size < 20:
        return audio
    chunk = audio[pos-size:pos].copy()
    fade = min(int(.005 * sr), size//4)
    if fade:
        ramp = np.linspace(0, 1, fade, dtype=np.float32)[:, None]
        chunk[:fade] *= ramp
        chunk[-fade:] *= ramp[::-1]
    return np.concatenate([audio[:pos], chunk, chunk, audio[pos:]], axis=0)


def _double_voice(audio, sr, amount, semitones):
    """原音に低めの声を薄く重ねる。amount=0〜0.6、元声が主役。"""
    if amount <= 0:
        return audio
    lower = _pitch_shift(audio, sr, semitones)
    # 原音100%を保持し、低い声を「追加」する。最終段で過大振幅のみ抑える。
    return audio + amount * lower


def _warble(audio, sr, level):
    """数ミリ秒だけ読み出し位置を揺らし、声質を微妙に不安定にする。"""
    if level <= 0 or len(audio) < 2:
        return audio
    t = np.arange(len(audio), dtype=np.float64) / sr
    delay = (.0008 + .0004 * level) * np.sin(2*np.pi*(3.0+.4*level)*t)
    delay += .0002 * level * np.sin(2*np.pi*1.3*t+.8)
    places = np.clip(np.arange(len(audio)) + delay*sr, 0, len(audio)-1)
    original_idx = np.arange(len(audio))
    altered = np.stack([np.interp(places, original_idx, audio[:,ch]) for ch in range(audio.shape[1])],axis=1)
    mix = min(.55, .18 + .12*level)
    return ((1-mix)*audio + mix*altered).astype(np.float32)


def _tail_drop(audio, sr, level):
    """声の終わり約0.3〜0.6秒を少し低くする。末尾に無音があると効きにくい。"""
    if level <= 0 or len(audio) < 256:
        return audio
    # 語尾に長い無音があっても声の終端を狙えるよう、有声音量から範囲を決める。
    envelope = np.max(np.abs(audio), axis=1)
    active = np.flatnonzero(envelope > max(.004, np.max(envelope)*.025))
    if len(active) == 0:
        return audio
    end = min(len(audio), int(active[-1])+1)
    length = min(end, int(sr*(.35+.09*level)))
    if length < 256:
        return audio
    start = end-length
    tail = audio[start:end].copy()
    low = _pitch_shift(tail, sr, -min(6, 1.4*level))
    fade = np.linspace(0, 1, length, dtype=np.float32)[:, None] ** 1.5
    result = audio.copy()
    result[start:end] = (1-fade)*tail + fade*low[:length]
    return result


def _ghost_echo(audio, sr, amount):
    """約95ms遅れの小さな声を重ねる。長い残響やステレオ移動は入れない。"""
    if amount <= 0:
        return audio
    delay = max(1, int(.095*sr))
    echo = np.zeros_like(audio)
    if delay < len(audio):
        echo[delay:] = audio[:-delay]
    return audio + amount*echo


def validate_fx(fx):
    """JSONのエフェクト範囲を検査。不明なキーは誤記として止める。"""
    limits = {'doubleVoice': (0,.6), 'doublePitch': (-9,0), 'warble':(0,9),
              'tailDrop':(0,9),'ghostEcho':(0,.5),'stutterAt':(0,1)}
    if not isinstance(fx,dict):
        raise ValueError('fxはオブジェクトで指定')
    for key,value in fx.items():
        if key not in limits: raise ValueError(f'未対応FX: {key}')
        if isinstance(value,bool) or not isinstance(value,(int,float)) or not np.isfinite(value):
            raise ValueError(f'FX値が不正: {key}')
        a,b = limits[key]
        if not a<=value<=b: raise ValueError(f'FX範囲外: {key}={value} (許可 {a}〜{b})')


def process(src, dst, noise, fx, seed):
    """生成済み音声を保存する。noise=0のみ許可し、無加工なら波形値を変更しない。"""
    if noise != 0:
        raise ValueError('V7ではノイズ加工は廃止。noise.level=0を指定してください')
    validate_fx(fx)
    audio, sr = sf.read(str(src), always_2d=True, dtype='float32')
    if len(audio)==0: raise ValueError('空のWAVは加工できません')
    if not fx:
        # 母親の声は原音のまま、ゲーム素材用PCM16 WAVにして保存する。
        sf.write(str(dst),audio,sr,subtype='PCM_16')
        return
    if fx.get('stutterAt') is not None:
        audio = _stutter(audio, sr, fx['stutterAt'])
    audio = _double_voice(audio, sr, fx.get('doubleVoice',0), fx.get('doublePitch',-4))
    audio = _warble(audio, sr, fx.get('warble',0))
    audio = _tail_drop(audio, sr, fx.get('tailDrop',0))
    audio = _ghost_echo(audio, sr, fx.get('ghostEcho',0))
    # 音声をつぶすコンプレッサーは使わず、音割れ時のみ全体音量を下げる。
    audio = np.nan_to_num(audio, nan=0.0, posinf=0.0, neginf=0.0)
    peak = np.max(np.abs(audio))
    if peak > .97: audio *= .97 / peak
    sf.write(str(dst),audio,sr,subtype='PCM_16')
