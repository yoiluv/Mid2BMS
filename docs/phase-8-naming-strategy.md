# Phase 8: Naming Strategy と Legacy 命名の維持

## 結果

`IKeySoundNamingStrategy.GetFileName(KeySoundContext)` を導入し、従来 `NameWaves` にあったキー音ファイル名の計算を `LegacyKeySoundNamingStrategy` へ移した。`KeySoundContext` はトラック名、モード、トラック内の順序、和音・OneShot フラグ、MIDI 由来のノート情報、出力の接頭辞・拡張子を持つ。

`NameWaves` はStrategyの結果をBMS側のWAV名とWaveSplitter側の出力名に用い、従来の `text5_renamer_array.txt` とPurple用ダミー枠を組み立てる。`MelodyWalker` のデフォルトStrategyはLegacyで、UIや設定は変更していない。Phase 7 の `KeySoundManifest` が両側の名前の一致を引き続き検証する。

Legacy Strategyに移した式は、Blue、Purple（直前のノートを含む）、Red（5桁の順番を含む）、和音、OneShot、および既存の予約済み `namingway` の動作を維持する。連番・トラック別連番・カスタムテンプレートの実装とUI選択はPhase 9以降に残す。

## 検証

- Coreテスト: 各モード、OneShot、和音、12半音の表記、予約済み `namingway`、Strategy注入時にBMS名とWaveSplitter名の両方へ反映されること。
- `scripts/test-regression.ps1`: CoreテストとGolden Master 7 fixture・91ファイルが一致。
- Release x86ビルドとWinForms起動スモークを確認。

既存の生成ファイル名、BMS定義、互換テキストおよびファイル集合は変更していない。
