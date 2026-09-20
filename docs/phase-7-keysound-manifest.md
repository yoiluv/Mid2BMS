# Phase 7: KeySound 内部モデルと Manifest

## 結果

`src/Mid2BMS.Core/KeySound/KeySoundManifest.cs` に、トラック・モード・キー音の順序・WAV ID・ファイル名・MIDI 由来の識別情報をまとめる内部モデルを導入した。識別情報はノート番号、ベロシティ、長さ、開始時刻、直前のノート番号のスナップショットであり、和音は複数ノートで表す。Red のように同じ音高でも別の音を作る場合は、トラック内のキー音 ID と WAV ID で区別する。

```text
MidInterpreter3 のノート / 和音
  → NameWaves の既存命名結果
  → KeySoundManifest
      ├→ BMSPlacement: #WAV 定義と配置に使う WAV ID
      ├→ text5_renamer_array.txt: 従来形式の互換出力
      └→ WaveSplitService: 従来形式を Manifest に復元し、WaveSplitter2 に渡す
```

変換中は `MelodyWalker.Manifest` が各トラックの `KeySoundTrack` を保持する。`BMSPlacement` は名前配列から独自に WAV ID を割り当てず、Manifest の名前・WAV ID を使用する。WaveSplitter は別操作として起動するため、`WaveSplitService` は互換ファイル `text5_renamer_array.txt` を同じ Manifest 型へ復元してから使用する。BMS 名と WAV 出力名が異なる場合は、変換時に拒否する。

新しい出力ファイルは増やしていない。`text5_renamer_array.txt` は従来と同じ内容で残し、WaveSplitter2 の低レベルな配列 API も変更していない。命名式そのものは `NameWaves` に残し、Naming Strategy への分離は Phase 8 とする。

## 検証

- Core テスト: ノート識別情報、WAV ID、Purple のダミー枠、空の和音トラック、互換形式の往復、名前の不一致拒否、Manifest 経由の WAV 分割。
- `scripts/test-regression.ps1`: Core テストと Golden Master 7 fixture・91 ファイルが一致。
- Release x86 ビルド、WinForms 起動スモークを確認。

## 境界

別セッションで `text5_renamer_array.txt` から復元する場合、従来形式に存在しない MIDI 識別情報・WAV ID・元の MIDI トラック番号は復元できない。Manifest には未知値として保持し、WaveSplitter が必要とする入力・出力名と順序だけを復元する。また、変換後に互換ファイルを手で変更した場合、既に書き出した BMS の `#WAV` 定義との整合性まではこの Phase では再検証しない。永続化形式や編集ワークフローの変更は、既存出力を変えない今回の対象外とした。
