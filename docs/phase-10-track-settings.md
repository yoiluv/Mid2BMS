# Phase 10: TrackSettings / TrackMode モデル

## 結果

`src/Mid2BMS.Core/Tracks/TrackSettings.cs` に `TrackMode` と `TrackSettings` を追加した。

```text
TrackSettings
 ├ Mode: Blue / Purple / Red
 ├ IsDrums
 ├ IsChord
 ├ IsOneShot
 ├ IsXChain
 └ Ignore
```

`Form1` とトラック確認画面 `Form2` の再変換処理は、5本の `List<bool>` ではなく `IReadOnlyList<TrackSettings>` を受け渡す。`Mid2BmsConverter` と `MelodyWalker` も同じモデルを使用し、通常変換、無視判定、Chord、OneShot、Red MIDI生成時のDrums・XChain等の設定を参照する。`KeySound` / 命名Contextのモードも同じ `TrackMode` に統一した。

既存の `MyForm.Mid2BMS_Process` のフラグ配列版は削除せず、`TrackSettings.FromLegacyFlags` を通す互換入口として残した。各フラグが `null` の場合は従来どおり全て `false` となる。既存global modeの `isRedMode` / `isPurpleMode` も残し、そこから全トラック共通のModeを作る。

Phase 10ではトラック別Modeの処理自体は有効にしていない。渡された設定数がMIDIトラック数と異なる場合、およびトラックのModeがglobal modeと異なる場合は明示的な例外にする。BlueとPurpleの混在処理はPhase 11、RedとSequenceLayerを含む混在処理はPhase 12以降の対象とする。画面のMode列追加も、混在変換が可能になる後続Phaseまで行わない。

## 検証

- Coreテスト: global boolからBlue/Purple/Redへの変換、旧フラグ配列の集約、`null`時の既定値、Phase 10での混在拒否。
- Golden Master: Legacy入口を使う既存7 fixture・91ファイルが一致。
- 新命名の実変換6シナリオ: `TrackSettings`入口を使い、Blue/Purple/Red/ChordとBMS・WaveSplitterの同期を確認。
- Debug / Release x86ビルドとWinForms起動スモークを確認。
