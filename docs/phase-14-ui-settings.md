# Phase 14: UI整理と設定保存

## 結果

通常のWinForms操作から、Phase 9～13で追加した命名方式とトラック別Modeを選択できるようにした。

Mid2MMLタブに`File Naming`を追加し、次の3方式を選択できる。

- `Legacy`: 従来互換の音高・velocity・長さベースの名前
- `Sequential`: 変換全体を通した`0001.wav`形式
- `Track + Sequential`: `Kick_001.wav`のようなトラック名＋トラック内連番

選択値は既存の`ctrl.json`保存対象に含まれる。古い`ctrl.json`に項目がない場合は従来互換の`Legacy`を使用する。

## トラック別Mode

変換後のトラック確認画面で「変更する」を選ぶと、各トラックに`Mode`のComboBoxが表示される。初期値にはMid2MMLタブのBlue / Purple / Redを使用し、行ごとに別のModeへ変更できる。

同じ表では既存の次の設定も編集できる。

- Track Name
- Drums
- OneShot
- Chord
- Ignore
- XChain（SequenceLayer有効時）

適用した内容は`IReadOnlyList<TrackSettings>`としてPhase 13の統合変換入口へ渡され、Blue / Purple / Red混在で再変換される。Red + OneShotもCoreの対応内容に合わせて選択可能にした。

## UI上の制約

Mode変更時に、成立しないセルを行単位で無効化する。

- PurpleではChordを無効化する。
- XChainはSequenceLayer有効時のRedトラックだけで有効にする。
- Chord + Drums、Ignore + その他、XChain + その他は適用時にも検証する。

画面側の制約を回避した入力に対しても、Coreの`TrackSettings.NormalizeForConversion`による検証は引き続き残る。

## Track Name metadata

`Kick [B]`のようなTrack Name metadataによる自動判定は任意要件のため、今回は追加していない。まずGUI上で明示的にModeを選べることを優先した。Track Name自体は従来どおり画面から編集でき、`Track + Sequential`のファイル名へ反映される。

## 検証

回帰テストに次のUI契約を追加した。

- File Namingの3項目が各Naming Strategyへ対応する。
- Red + OneShotを許可する。
- Purple + Chordを拒否する。
- XChainをRed + SequenceLayerの場合だけ許可する。

既存Golden Masterと混在Modeシナリオは`scripts/test-regression.ps1`で引き続き検証する。
