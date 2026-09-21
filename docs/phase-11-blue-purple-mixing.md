# Phase 11: Blue / Purpleトラック混在

## 結果

`TrackSettings.Mode`をトラックごとの変換モードとして使用し、1回のCore変換でBlueトラックとPurpleトラックを混在できるようにした。

各トラックはそれぞれのModeに従って、キー音identity、Legacy命名の`b_` / `p_` prefix、Purpleの直前音ダミー、WaveSplitter用slot、BMS配置を生成する。単音化MIDIにもBlueの通常ノート列とPurpleの`previous note + current note`列がトラック単位で共存する。

既存のglobal `isRedMode` / `isPurpleMode`入口は維持する。`TrackSettings`が`null`の場合は従来どおりglobal modeを全トラックへ適用し、全Blue・全Purple・全Redの出力ファイル名と内容を変更しない。明示的な`TrackSettings`がある場合はBlue/Purpleについてトラック設定を優先する。

Blue/Purple混在時の出力は、単一モードの出力と区別するため次の名前にした。

```text
text3_tanon_smf_blue_purple.mid
text6_bms_blue_purple.txt
```

## 今回の境界

Phase 11ではRedを含む混在を許可しない。RedとBlue/Purpleの混在、Blue/Purple混在時のSequenceLayerは明示的な例外とし、Phase 12へ残した。PurpleとChordの組み合わせも既存アルゴリズムが対応しないため、混在設定では変換前に拒否する。

画面にはトラック別Mode列を追加していない。通常のWinForms操作は従来どおりglobal modeを使用し、混在機能は`IReadOnlyList<TrackSettings>`を受け取るCore入口から利用できる。UI整理と設定保存はPhase 14の対象とする。

## 検証

- Coreテスト: Blue/Purple混在の受理、Red混在の拒否、混在SequenceLayerの拒否、Purple + Chordの拒否。
- Golden Master: 既存7 fixture・91ファイルが一致。
- 既存の命名シナリオ6件が一致。
- Blue/Purple混在シナリオ: 4つの音楽トラックをBlue/Purpleへ交互に設定し、混在用BMS・MIDI、`b_` / `p_` WAV定義、Purpleダミーslot、単音MIDIのノート数を確認。
