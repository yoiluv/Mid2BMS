# Phase 2: Golden Master / regression test

## 結果

現行のlegacy pipelineを変更せず、MIDI入力から生成される中間ファイル、単音化MIDI、renamer情報、BMS、track CSVをbyte単位で比較するcharacterization testを追加した。

テストはWinFormsを起動せず、`MyForm.Mid2BMS_Process`を入口にして実際の次の経路を通る。

```text
MyForm.Mid2BMS_Process
  -> Mid2mml.Process
  -> MelodyWalker.MultiProcess
  -> MidInterpreter / MidInterpreter2 / MidInterpreter3
  -> NameWaves
  -> BMSPlacement
```

## 実行方法

リポジトリルートからPowerShellで実行する。

```powershell
.\scripts\test-regression.ps1
```

スクリプトは次を順に行い、差異があればexit code 1を返す。

1. `Debug|x86`でアプリケーションとcharacterization runnerをビルド
2. fixtureごとに独立した一時directoryを作成
3. `input.mid`を固定設定でlegacy pipelineへ入力
4. 生成されたファイル名の集合を`expected/`と比較
5. 各ファイルをbyte単位で比較

`encoding.ini`と実行中の生成物はOSの一時directoryへ隔離され、fixtureや通常の作業directoryを上書きしない。成功時は一時directoryを削除し、失敗時は調査用にactual出力の場所を表示して保持する。

## fixture matrix

| Fixture | 入力 | Mode / 設定 | 目的 |
|---|---|---|---|
| `blue_basic` | 既存Blue sample | Blue標準 | 基本の単音化、命名、BMS配置 |
| `purple_portamento` | 既存Blue sample | Purple | Purpleの命名・配置経路 |
| `red_automation` | 既存Red sample | Red標準 | automationを含むRed変換 |
| `chord` | 既存Blue sample | 全非conductor trackをChord | 同時発音のgroup化 |
| `drums` | 既存Blue sample | 全非conductor trackをDrums | note別lane割り当て |
| `oneshot` | 既存Red sample | SequenceLayer + OneShot | global/one-shot automation |
| `sequence_layer` | 既存Red sample | SequenceLayer | trackの時間方向合成 |

全fixtureはUIの既定値に合わせ、開始WAV ID `01`、開始BMS channel index `16`、margin `12.0`拍、WAV ID spacing `0`、timebase `48`、velocity step `1`を明示している。追加生成物は回帰範囲を広げるため有効にした。

## Golden Masterの内容

各fixtureの`expected/`には13ファイル、全7 fixtureで91ファイルを保持する。

- `midiinput_timequantizedstream.mid`
- `text0_stdout_part1.txt`
- `text0_stdout_part1_array.txt`
- `text1_midtable_debug.txt`
- `text2_mmlrenew_debug.txt`
- `text3_tanon.mml`
- `text3_tanon_smf_<mode>.mid`
- `text4_renamer.txt`
- `text5_renamer_array.txt`
- `text6_bms_<mode>.txt`
- `text6_tempochangebms.txt`
- `text9_trackname_csv.txt`
- `wavesplitter_input.txt`

比較では、ファイルの追加・欠落と内容変更の両方を失敗として扱う。内容が異なる場合はexpected/actualのbyte数とSHA-256を表示する。

## Golden Masterの更新

出力変更が意図されたもので、レビュー可能な状態になった場合のみ次を実行する。

```powershell
.\scripts\test-regression.ps1 -Accept
```

この操作は全fixtureの`expected/`を現行出力で置換する。実行後は通常モードを再実行し、BMS、MIDI、renamer、CSVの差分をcommit前に確認する。

fixtureを追加する場合は、`tests/fixtures/<name>/`へ次を置く。

```text
fixture.properties
input.mid
expected/
```

`fixture.properties`でmode、track flag、UI相当の数値設定を固定する。runnerの再コンパイルなしで同じ形式のfixtureを追加できる。

## 検証結果

2026-09-18に.NET Framework 4.0 / Debug x86で全fixtureを実行し、91ファイルが採取直後のGolden Masterと完全一致することを確認した。

## 現時点の境界

- Golden MasterはPhase 2開始時点の「現行挙動」を固定するものであり、音楽的な正しさを新たに保証するものではない。
- Purple fixtureは既存Blue sampleからPurple分岐を通している。専用のportamento MIDIが得られた場合はfixtureを追加・差し替える余地がある。
- WaveSplitterによる音声切り出し結果、GUI表示、設定保存は今回のbyte比較には含めていない。
- legacy code内の`MessageBox`は変更していない。固定fixtureは確認dialogを発生させない入力として検証済みである。
