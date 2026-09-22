# Phase 13: Blue / Purple / Red統合ワークフロー

## 結果

Blue / Purple / Redの変換設定、SequenceLayer、命名に必要な入力を`Mid2BmsConversionRequest`へ集約し、Core変換の正式な入口を一つにした。変換結果は`Mid2BmsConversionResult`として返し、呼び出し側は`ref`と`out`の組み合わせやglobal modeのboolを組み立てなくても変換を実行できる。

```csharp
var request = new Mid2BmsConversionRequest
{
    DefaultTrackMode = TrackMode.Blue,
    TrackSettings = trackSettings,
    CreateExtraFiles = true,
    MarginTimeBeats = "12.0",
    SequenceLayer = true,
    NewTimebase = 48,
    VelocityStep = 1,
    StartingWavId = 1,
    StartingBmsChannelIndex = 16,
};

Mid2BmsConversionResult result = workflow.Mid2BMS_Process(
    request, ref progressValue, ref progressFinished);
```

`DefaultTrackMode`は、初回解析のように`TrackSettings`がまだ存在しない場合だけ全トラックへ適用する。明示的な`TrackSettings`がある場合は各トラックのModeを唯一の変換判断として使用する。

## 統合した処理

`SingleNoteMidiBuilder`を追加し、単音化MIDIの生成と出力を一つの経路へまとめた。

```text
Mid2BmsConversionRequest
  -> TrackSettingsを解決
  -> MelodyWalkerでBMS / Manifest / Blue・Purple素材を生成
  -> SingleNoteMidiBuilder
       ├ Blue / Purpleのみ: 生成済みMIDI
       ├ Redのみ: 既存Red SplitNotes互換処理
       └ 混在: MixedModeMidiBuilder
  -> text3_tanon_smf_<mode-set>.midを一度だけ出力
  -> Mid2BmsConversionResult
```

Phase 12まではBlue / Purple MIDIを`MelodyWalker`、Redと混在MIDIを`Mid2BmsConverter`が別々に書き出していた。Phase 13では`MelodyWalker`は生成結果を返すだけとし、ファイル出力の判断を`SingleNoteMidiBuilder`経由の一箇所へ集約した。

## Resultの内容

`Mid2BmsConversionResult`から次を取得できる。

- 解決済み`TrackSettings`
- 使用したMode suffix
- 単音化MIDIのパス
- BMSのパス
- KeySoundManifest互換ファイルのパス
- Track CSV
- MIDI Track Name / Instrument Name
- 次に利用可能なWAV ID / BMS channel index
- 上書き確認でキャンセルされたかどうか

これにより後続処理やPhase 14のUIは、Modeの組み合わせから出力ファイル名を再計算する必要がない。

## 互換入口

従来の`isRedMode` / `isPurpleMode`および複数の`List<bool>`を受け取る`MyForm.Mid2BMS_Process`は削除していない。旧入口は値を`TrackMode`と`Mid2BmsConversionRequest`へ変換し、同じ統合ワークフローを呼ぶアダプターとして残した。

WinFormsの通常変換も新しいRequest / Result入口へ切り替えた。現在のBlue / Purple / Redラジオボタンは初回解析時の`DefaultTrackMode`を決めるためにだけ使用する。トラック確認後の再変換では`Form2`が返す`TrackSettings`を優先する。

## 検証

`scripts/test-regression.ps1`で次を確認した。

- Core isolation testが成功。
- 既存Golden Master 7 fixture・91ファイルが一致。
- 従来のbool / flag入口を使用する単一Mode fixtureがすべて一致。
- 新しいRequest / Result入口を使用するBlue / Purple混在、Blue / Red混在、混在SequenceLayerが成功。
- Blue / Purple / Red + SequenceLayer + XChainが成功。
- ResultがMIDI、BMS、Manifestの実在するパスと解決済みTrackSettingsを返す。

## 現在の境界

Phase 13は変換ワークフローとCore契約の統合であり、トラック別Modeを編集するUIは追加していない。Mode列、命名方式の選択、設定保存はPhase 14で扱う。
