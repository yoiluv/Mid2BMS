# Phase 9: 連番命名と BMS / WaveSplitter の同期

## 結果

`IKeySoundNamingStrategy` に `SequentialKeySoundNamingStrategy` と `TrackSequentialKeySoundNamingStrategy` を追加した。従来の `LegacyKeySoundNamingStrategy` が引き続きデフォルトであり、Golden Masterの出力は変えていない。

- `Sequential`: 変換全体で `0001.wav`, `0002.wav`, … と採番する。空トラックやWAV IDのトラック間スペースでは番号を飛ばさない。
- `TrackSequential`: トラックごとに `Kick_001.wav`, `Piano_001.wav`, … と採番し直す。ファイル名に使えない文字は `_` に置き換え、同名トラックには元のトラック番号を付加する。

選ばれたStrategyは `MyForm.NamingStrategy → Mid2BmsConverter → MelodyWalker → NameWaves` に渡る。`NameWaves` が生成した同じ名前を `KeySoundManifest` がBMSの `#WAV` 定義と `text5_renamer_array.txt` に投影するため、連番時もBMS定義を手で書き換える必要はない。WaveSplitterタブでは従来どおりリネームを有効にすると互換ファイルを読み込み、連番名のWAVを書き出す。

トラック名の重複・大文字小文字差・Windowsで使えない文字による衝突を防ぐため、非Legacy方式では最終的な出力名の重複をチェックし、衝突時はBMSと互換ファイルの書き出し前にエラーにする。WaveSplitterのダミー枠を表す `____dummy_` で始まるトラック名も出力名では回避する。

## 検証

`scripts/test-regression.ps1` でCoreテスト、Golden Master 7 fixture・91ファイルの一致、追加の実変換シナリオ6件を確認した。追加シナリオはBlue/Purple/Red/Chord、複数トラックの通し番号、同名トラック、WAV IDの開始値とトラック間スペース、`#WAV`名とWaveSplitter名の一致を含む。合成WAVを用いた音切りでも、BMSが定義した `0001.wav` が実際に出力されることを確認した。Release x86ビルドとWinForms起動スモークも確認した。

## 今回の境界

画面から命名方式を選ぶUIと設定保存はPhase 14に残す。現時点ではCore経由でStrategyを指定でき、通常の画面操作はLegacyのままである。WaveSplitterタブ単独の「リネーム無効＋連番」経路は従来動作を維持し、BMSと同期する新方式を使う場合はMIDI変換側でStrategyを指定してリネーム有効で音切りする。変換後に互換ファイルを手で編集した場合のBMS整合性再検証もPhase 7と同じく対象外とした。
