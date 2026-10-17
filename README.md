# gamejam

## セットアップ

1. **Unity 6000.6.6f1** を Unity Hub でインストール（バージョンを揃えること）
2. Git と Git LFS を入れる（Git for Windows / GitHub Desktop なら LFS 同梱）
3. クローンして Unity Hub の「Add project from disk」でこのフォルダを開く

```bash
git lfs install
git clone <このリポジトリのURL>
```

初回は `Library/` の再生成で数分かかります。

## ゲーム：まねごえの森（仮）

森で迷子になったカイが、お母さんの声を頼りに森を抜ける一人称ホラー（WebGL）。
遊び方・AI・声の仕組み・素材の作り直し方は [Docs/KaiForest.md](Docs/KaiForest.md) を参照。

- WebGL で書き出すには Unity に **WebGL Build Support** モジュールが必要
- シーンは2つ。どちらを開いて ▶ を押しても遊べる
  - `Assets/Scenes/Forest.unity`：**マップ**。木・目印・鳥居・スタート地点が全部ふつうのオブジェクトとして置いてあるので、エディタで直接編集する
  - `Assets/Scenes/Main.unity`：ゲーム進行（GameManager とポストエフェクトだけ）。`KaiForest > Setup Project` で上書きされるので**手で編集しない**
- `KaiForest > Build WebGL` / `Build Windows` で `Builds/` に書き出す

## ルール

- `.meta` ファイルは必ずアセットとセットでコミットする
- 同じシーン（`.unity`）や Prefab を同時に編集しない。触る前にチームに一声かける
- 作業は自分用のシーン / Prefab で行い、メインシーンへの統合は担当者を決める
- 作業前に `git pull`、こまめに commit & push
