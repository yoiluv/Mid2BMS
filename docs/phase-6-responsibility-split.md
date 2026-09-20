# Phase 6: MyForm の責務分割

## 結果

`MyForm` に混在していた変換・ファイル処理を `Mid2BMS.Core` の小さなサービスへ移した。WinForms 側には入力済みパラメータの受け渡し、上書き確認、処理結果の表示を残している。呼び出し方向は `UI → Application Service → 既存 Core 処理` であり、DI フレームワークや新しいデータモデルは導入していない。

| 責務 | 移動先 | 主な内容 |
| --- | --- | --- |
| MIDI から BMS までの変換手順 | `Services/Mid2BmsConverter.cs` | MIDI 解析、重複ノート確認、MML と BMS の生成、Red Mode の MIDI 出力 |
| MIDI の時間分解能・ベロシティ量子化 | `Midi/MidiQuantizer.cs` | 既存の 2 つの量子化処理。画面の単独実行ボタンからも共用 |
| 生成済みファイルの上書き判定 | `Services/GeneratedFileHashGuard.cs` | MD5 と `hashvalues1.txt` / `hashvalues2.txt` の照合。確認ダイアログは UI のコールバック |
| WAV の音切り | `Services/WaveSplitService.cs` | 入力・リネーム一覧の構築と `WaveSplitter2` の実行 |
| BMS の重複定義処理 | `Services/DuplicateDefinitionService.cs` | BMS の読み込み、`DupeDefinition` の実行、結果の書き込み |

`MyForm` の既存の公開入口 (`Mid2BMS_Process`、`WaveSplit_Process`、`DupeDef_Process`) と進捗更新・例外処理の境界を保った。`Mid2BmsConverter` 内のユーザー確認や通知には Phase 5 の `CoreInteraction` を使用する。従来から `if (false)` で無効だったハッシュ書き込みと、常に `false` だった重複定義の追加ファイル出力は、実行されないコードとして除去した。

## 検証

`scripts/test-regression.ps1` で Core 単独テストと Golden Master 7 fixture・91 ファイルの一致を確認した。Core テストには MD5 上書き判定、時間分解能・ベロシティ量子化、WAV 分割、重複定義出力の確認を追加した。Release x86 ビルドと WinForms の起動スモークも確認した。

## 残す範囲

`Mid2BmsConverter` の引数と一部の静的状態は既存処理に合わせて維持している。`MelodyWalker` 等の内部アルゴリズムには手を加えていない。`KeySoundManifest` や命名戦略の導入は、それぞれ Phase 7 / Phase 8 の対象とする。Golden Master が対象としない WAV 分割や重複定義については小さなサービス単位のテストを追加したが、実 GUI の操作網羅や OGG 経路の検証は含まない。
