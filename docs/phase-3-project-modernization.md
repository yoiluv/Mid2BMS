# Phase 3: .NET Framework / project file modernization

## 結果

アプリケーションとcharacterization testを`.NET Framework 4.8`へretargetし、両projectをSDK-styleへ移行した。アプリケーションのC#コードと変換アルゴリズムには変更を加えていない。

```text
旧: ToolsVersion 4.0 project / .NET Framework 4.0
新: Microsoft.NET.Sdk project / .NET Framework 4.8 (net48)
```

`Microsoft.NETFramework.ReferenceAssemblies` `1.0.3`は開発専用依存として維持している。このため、参照アセンブリをNuGet restoreで固定するPhase 1の再現性は失われない。

## project構成

SDK-styleのdefault item収集を利用し、旧projectに列挙されていた73個のC# sourceを自動的にcompileする。旧projectで意図的に除外されていた次の代替実装は`Compile Remove`で引き続き除外する。

- `DynamicJson.cs`
- `WaveSplitter/IWaveSplitter.cs`
- `WaveSplitter/WaveSplitter.cs`

WinForms designer、`.resx`、`Settings.settings`の関連付けはproject内に明示した。`culture.ini`と`_Docs/mid2bms_history.txt`の出力directoryへのコピー、application icon、`NVorbis.dll`参照も維持している。

## compatibility設定

| 項目 | Phase 3後の設定 |
|---|---|
| Target framework | `net48` |
| Debug solution platform | `x86` |
| Debug `PlatformTarget` | `x86` |
| Release solution platform | `x86` |
| Release `PlatformTarget` | `AnyCPU`（既存設定を維持） |
| Debug output | `Mid2BMS/bin/Debug/` |
| Release output | `Mid2BMS/bin/Release/` |

SDK既定のframework/platform別subdirectoryは無効化し、既存のscriptと配布手順が参照する出力pathを維持した。

`app.config`とtest runnerの`app.config`は、CLR v4を使用したまま対象SKUを`.NETFramework,Version=v4.8`へ更新した。

## build

従来のrepository scriptをそのまま利用できる。

```powershell
.\scripts\build-legacy.ps1
.\scripts\build-legacy.ps1 -Configuration Release
```

SDK-style projectになったため、.NET CLIからもbuildできる。

```powershell
dotnet restore .\Mid2BMS.sln -p:Platform=x86
dotnet build .\Mid2BMS.sln --no-restore -c Debug -p:Platform=x86
```

最初のrestoreではNuGet.orgへの接続が必要になる。

## regression test

```powershell
.\scripts\test-regression.ps1
```

Phase 2の7 fixture、91生成物を.NET Framework 4.0時点のGolden Masterと比較し、すべてbyte単位で一致することを確認した。

移行前検証では、Golden Masterのtext fileがGitの既定改行変換を受ける問題も検出した。fixtureの入力とexpected出力を`.gitattributes`でbinary扱いに固定し、`.NET Framework 4.0`版でbaselineを再採取してからretargetを実施している。

## 検証結果

2026-09-18に次を確認した。

- Visual Studio 2022 / MSBuild 17: Debug x86 build成功
- Visual Studio 2026 / MSBuild 18: Debug x86 / Release build成功
- .NET SDK 10.0.200: restore / Debug x86 build成功
- Debug executable: `32BITREQ=1`
- Release executable: `32BITREQ=0`
- app/test runnerのtarget framework attribute: `.NETFramework,Version=v4.8`
- Debug WinForms executable: startup後3秒間の継続動作を確認
- Golden Master: 7 fixture、91ファイル完全一致
- 既存compile warning: 9件のまま

## Phase 3の境界

Phase 3ではproject systemと.NET Framework targetだけを変更した。modern .NETへの移行、`Mid2BMS.Core`分離、class責務分割、アルゴリズム修正は行っていない。これらはPhase 4以降でGolden Masterを通しながら段階的に扱う。

旧projectに含まれていたClickOnce/bootstrapper metadataは、repositoryにpublish profileや配布scriptが存在しないためSDK-style projectへ持ち込んでいない。通常のDebug/Release buildと実行ファイル構成は検証済みだが、ClickOnce publishはPhase 3の検証対象外である。
