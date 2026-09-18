# Characterization fixtures

各directoryの`input.mid`を現行のlegacy pipelineへ渡し、`expected/`内の全ファイルとbyte単位で比較する。

- `fixture.properties`: modeおよびUI相当の固定設定
- `input.mid`: repositoryに元から存在するBlue/Red sampleをfixtureごとに固定した入力
- `expected/`: Phase 2開始時点のlegacy実装から採取したGolden Master

通常の検証:

```powershell
.\scripts\test-regression.ps1
```

意図した出力変更後にGolden Masterを更新する場合:

```powershell
.\scripts\test-regression.ps1 -Accept
```

`-Accept`は全fixtureのexpectedファイルを置換するため、差分を必ず確認すること。
