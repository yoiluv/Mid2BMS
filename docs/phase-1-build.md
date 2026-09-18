# Phase 1: 現行版のビルド環境確立

> この文書はPhase 1完了時点の`.NET Framework 4.0`環境を記録したもの。Phase 3以降の現在の構成は[phase-3-project-modernization.md](phase-3-project-modernization.md)を参照。

## 結果

現行のターゲットフレームワークとソースコードを変更せず、`Debug|x86` を再現可能にビルドできるようにした。

- ターゲット: `.NET Framework 4.0`
- 基準構成: `Debug|x86`
- ビルドツール: Visual Studio 2022以降、または同世代のBuild Toolsに含まれるMSBuild
- 参照アセンブリ: NuGetの`Microsoft.NETFramework.ReferenceAssemblies` `1.0.3`
- 出力: `Mid2BMS/bin/Debug/Mid2BMS.exe`

`.NET Framework 4.0 Developer Pack`や古いWindows SDKをマシンへ別途導入する必要はない。参照アセンブリは開発専用のNuGet依存として復元し、アプリケーションの配布物には含めない。

## ビルド方法

リポジトリルートからPowerShellで次を実行する。

```powershell
.\scripts\build-legacy.ps1
```

スクリプトは`vswhere`を使って最新のVisual Studio/Build ToolsのMSBuildを検出し、パッケージ復元後にsolutionをビルドする。最初の実行時はNuGet.orgへの接続が必要になる。

復元済みの環境でネットワークを使わず再ビルドする場合:

```powershell
.\scripts\build-legacy.ps1 -NoRestore
```

Releaseを確認する場合:

```powershell
.\scripts\build-legacy.ps1 -Configuration Release
```

ただし、現行projectでは`Release|x86`構成の`PlatformTarget`が`AnyCPU`になっている。Phase 1の互換基準は構成と実際のtargetがともにx86である`Debug|x86`とし、この不一致は既存挙動として維持する。

## project変更

`Mid2BMS.csproj`へ次だけを追加した。

1. 旧形式projectでもNuGet restoreの方式を明示する`RestoreProjectStyle=PackageReference`
2. `Microsoft.NETFramework.ReferenceAssemblies` `1.0.3`への開発専用参照

アプリケーションコード、target framework、platform設定、外部DLL参照には変更を加えていない。

## 検証結果

2026-09-18に次の環境で`Debug|x86`のrestoreとbuildが成功した。

| MSBuild | 結果 | 出力 |
|---|---|---|
| Visual Studio 2022 / MSBuild 17.13.19 | 成功 | `Mid2BMS.exe` |
| Visual Studio 2026 / MSBuild 18.4.0 | 成功 | `Mid2BMS.exe` |

いずれもコンパイル警告は9件で、内容は既存コードの未使用field、到達不能code、`Complex`の`Equals`/`GetHashCode`未実装に関するものだった。Phase 1では既存挙動を変えないため、警告の解消は行っていない。

スクリプト経由では`Release|x86`のビルドも成功した。`CorFlags`で生成物を確認した結果、Debugは`32BITREQ=1`、Releaseは`32BITREQ=0`で、既存project設定どおりである。

## Phase 1の完了条件

- [x] `.NET Framework 4.0`を維持
- [x] `Debug|x86`をクリーンなNuGet restoreからビルド可能
- [x] マシン固有の.NET Framework 4.0 reference assembliesに依存しない
- [x] ビルドコマンドをrepository内のscriptへ固定
- [x] VS 2022/2026のMSBuildで結果を確認
- [x] アプリケーションコードと出力仕様は未変更

GUI操作と既存sampleからのGolden Master採取は、仕様どおりPhase 2のregression test構築で扱う。
