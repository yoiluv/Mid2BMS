# Phase 5: CoreとWinForms UIの分離

## 結果

MIDI・BMS・音声処理、入出力、共通データ型を`src/Mid2BMS.Core`へ移し、Windows Desktopを必要としない`net10.0`ライブラリにした。既存の`Mid2BMS`プロジェクトは`net10.0-windows`のWinFormsアプリとして残し、Coreを参照する。

```text
Mid2BMS (WinForms, net10.0-windows)
    └── Mid2BMS.Core (net10.0)

Mid2BMS.CharacterizationTests ──┬── Mid2BMS (MyFormの既存回帰試験)
                                 └── Mid2BMS.Core
Mid2BMS.Core.Tests ───────────────── Mid2BMS.Core
```

CoreからWinFormsアセンブリおよびUIプロジェクトへの参照はない。Core単独のtest runnerはWindows Desktop Runtimeを要求しない。

## ソースの配置と互換性

`BMSParser`、`BMSUtil`、`IO`、`JCode`、`MelodyWalker`、`Mid2mml`、`MidiStruct`、`SignalProcessing`、`Util`などの既存ソースをCoreへ移した。`WaveSplitter2`、WAV/BMP処理、重複定義処理もCore側にある。フィルターのインパルス応答WAVと`Properties.Resources`もCoreへ移し、リソース名`Mid2BMS.Properties.Resources`を維持した。

既存の名前空間は変更せず、Coreの内部型をWinFormsホストと既存のテストから利用するため、限定的な`InternalsVisibleTo`を設定した。`Form1`、`Form2`、`Program`、`ControlProperty`、フォームの設定・リソースはWinForms側に残る。ダイアログを含む`MyForm`と`TinyTinyRenamer`も、責務分割を行うPhase 6まではUIプロジェクトに残す。

WinFormsプロジェクトのディレクトリ名、`Mid2BMS.exe`、Debug/Releaseの出力パスは変更していない。UI出力に`Mid2BMS.Core.dll`が追加される。`NVorbis.dll`は既存の配布物を維持するためUIプロジェクトから引き続きコピーするが、現行ソースから直接利用していない。

## UIとの境界

Coreに残っていた通知・確認ダイアログを`ICoreInteraction`経由に変更した。WinForms起動時に`WinFormsCoreInteraction`を登録するため、実際の`MessageBox`の文面・ボタン・確認結果は従来どおりである。ホスト未設定時に通知・確認が必要になった場合は例外とし、ライブラリが黙って選択することを防ぐ。回帰テストは独自の非対話ホストを登録する。

旧Silverlight向けの、現在はコンパイル対象外だった`FileStreamFactory`分岐を削除した。現行の`FileIO`/`neu`実装は維持している。

## 検証

2026-09-20に次を確認した。

- `src/Mid2BMS.Core`単独のDebug x86ビルド成功
- solution全体のDebug x86／Releaseビルド成功
- Core分離テスト成功（非Desktopターゲット、WinForms/UI参照なし、Shift_JIS、フィルターリソース、通知の受け渡し）
- Golden Master 7 fixture・91生成物がbyte単位で完全一致
- Debug WinFormsアプリの起動後3秒間の継続動作を確認
- UI出力内の`Mid2BMS.Core.dll`、`NVorbis.dll`、日本語リソースを確認

実行コマンドは従来どおりで、`test-regression.ps1`がCore分離テストとGolden Masterの両方を実行する。

```powershell
.\scripts\test-regression.ps1
.\scripts\build-legacy.ps1 -Configuration Release
```

## Phase 6へ残した内容

`MyForm`はまだ入力解析・変換・ファイル生成・ダイアログを束ねており、WinForms側に置いている。Phase 6ではこの処理を段階的にCoreのサービスへ移し、UI側を入力・進捗・確認に絞る。現在の`Mid2BMS`ディレクトリを最終案の`src/Mid2BMS.WinForms`へ改名する作業も、出力パスや既存スクリプトに影響するため今回の対象外とした。

既存の`NVorbis.dll`を使うOGG経路と実際の画面操作はGolden Masterに含まれていない。今回の検証は起動スモークと変換出力の回帰比較までである。
