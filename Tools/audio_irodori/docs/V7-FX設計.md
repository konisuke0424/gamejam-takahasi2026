# V7 エフェクト設計

1. **Irodori-TTS**：母親の参照WAVから同じ声を生成。妖怪だけ短いcaptionで声の濁り・低い響き・語尾を指示。
2. **doubleVoice**：原音を残して、少し低音化した声を小さく重ねる。
3. **warble**：微細な変調。原音の言葉の明瞭度を優先。
4. **tailDrop**：声の末尾だけ低音化。
5. **ghostEcho**：ごく短い遅延を低レベルで追加。左右の定位・長い残響は加えない。
6. **noise**：全件無効。

- `raw/` … Irodori 直後の元音
- `export/*.prefar.wav` … V7 FX 後（距離加工前）
- `export/*.wav` / `*_far.wav` … `game_voices.py` による near/far（Unity 用。sync で `Assets/Game/Audio/Voices` にコピー）

ゲーム本体の改修は禁止。
