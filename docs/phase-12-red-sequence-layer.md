# Phase 12: Red mode / SequenceLayer

## 結果

`TrackSettings.Mode`にRedを含む構成をCore変換で受理し、Blue / Purple / Redを同じ単音化MIDI、BMS、KeySoundManifestへ出力できるようにした。混在時もRedトラックのNote、Pitch Bend、CC等は量子化済みの元MIDIから切り出し、Blue / Purpleの生成トラックと統合する。

`SequenceLayer`を有効にした場合は、音を出すトラックを元のトラック順で共通時間軸へ並べ直す。Redのautomationは対応するRedノートと同じ移動量で配置する。Red + XChainのトラックはBMS・キー音生成から除外しつつ、サイドチェイントリガーとして単音化MIDIに残す。

## 出力名

単一モードの既存名は変更しない。

```text
text3_tanon_smf_blue.mid
text3_tanon_smf_purple.mid
text3_tanon_smf_red.mid
text6_bms_blue.txt
text6_bms_purple.txt
text6_bms_red.txt
```

混在時は含まれるモードをBlue、Purple、Redの順に連結する。

```text
text3_tanon_smf_blue_red.mid
text3_tanon_smf_blue_purple_red.mid
text6_bms_blue_red.txt
text6_bms_blue_purple_red.txt
```

## 実装

`MixedModeMidiBuilder`は、`MelodyWalker`が生成したBlue / Purpleトラックと、元MIDIから`MidiTrack.SplitNotes`で再構成したRedトラックを統合する。Red側のtimebaseは生成MIDIのtimebaseへ変換し、トラック数とトラック順を入力MIDIに合わせる。

Red分割のintervalとautomation前後幅は`RedSplitOptions`で変換ごとに渡す。混在変換では`MidiTrack`のstatic設定を書き換えないため、別の変換設定が暗黙に残ることを避けられる。既存入口は互換性のためstatic設定を`RedSplitOptions`へ変換して使用する。

全Redの場合は従来のRed出力経路をそのまま使用する。これにより既存のRed、SequenceLayer、OneShotのバイト単位のGolden Masterを維持する。

設定検証は次の規則とした。

- Blue / Purple / Redの混在を許可する。
- 混在構成でのSequenceLayerを許可する。
- Purple + Chordは既存アルゴリズムが対応しないため拒否する。
- XChainはRed + SequenceLayerの場合だけ許可する。
- Ignore、Red + Chord、Red + OneShotの既存動作を維持する。

## 検証

`scripts/test-regression.ps1`で次を確認した。

- Core isolation testが成功。
- 既存Golden Master 7 fixture・91ファイルが一致。
- 既存の命名シナリオとBlue / Purple混在が成功。
- Blue / Red混在で、BMS・Manifest・単音化MIDIに両モードが存在する。
- Blue / Red + SequenceLayerで、RedのCC / Pitch Bendを保持し、発音が重ならない。
- Blue / Purple / Red + SequenceLayer + XChainで、3モードのBMS定義、非重複配置、XChainトリガー保持を確認。

## 現在の境界

トラック別Modeを選択するWinForms UIはまだ追加していない。Phase 12の機能は`IReadOnlyList<TrackSettings>`を受け取るCore入口から利用できる。既存UIは引き続きプロジェクト全体のBlue / Purple / Redを使用する。

Phase 13ではglobal `isRedMode` / `isPurpleMode`を変換判断の中心から外し、混在変換を一つの正式なワークフローとして整理する。UIのMode列と設定保存はPhase 14で扱う。
