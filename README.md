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

## ルール

- `.meta` ファイルは必ずアセットとセットでコミットする
- 同じシーン（`.unity`）や Prefab を同時に編集しない。触る前にチームに一声かける
- 作業は自分用のシーン / Prefab で行い、メインシーンへの統合は担当者を決める
- 作業前に `git pull`、こまめに commit & push
