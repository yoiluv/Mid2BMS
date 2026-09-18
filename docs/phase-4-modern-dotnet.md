# Phase 4: modern .NETへの移行

## 結果

WinFormsアプリケーションとcharacterization testを`.NET Framework 4.8`から`.NET 10`へ移行した。

```text
旧: net48
新: net10.0-windows
```

変換アルゴリズムやGolden Masterは変更していない。Phase 2で固定した7 fixture、91生成物は、移行後もすべてbyte単位で一致する。

## project設定

アプリケーションとtest runnerの両方を`net10.0-windows`へretargetした。SDK-style project、既存のsolution platform、出力directoryは維持している。

| 項目 | Phase 4後の設定 |
|---|---|
| Target framework | `net10.0-windows` |
| Debug solution platform | `x86` |
| Debug `PlatformTarget` | `x86` |
| Release solution platform | `x86` |
| Release `PlatformTarget` | `AnyCPU`（既存設定を維持） |
| Debug output | `Mid2BMS/bin/Debug/` |
| Release output | `Mid2BMS/bin/Release/` |

Debugのapphostはx86、managed assemblyは`32BITREQ=1`となる。Releaseのmanaged assemblyは`32BITREQ=0`のAnyCPUで、現在のWindows x64 SDKが生成するapphostはx64となる。

`.NET Framework`用の`app.config`は削除した。実行時frameworkはbuildで生成される`Mid2BMS.runtimeconfig.json`に記録され、`Microsoft.NETCore.App 10.0.0`と`Microsoft.WindowsDesktop.App 10.0.0`を使用する。

追加のNuGet packageは必要ない。`NVorbis.dll`は既存の直接参照を維持している。

## compatibility対応

modern .NETで従来の動作を保つため、次の限定的な変更を行った。

- Shift_JIS取得前に`CodePagesEncodingProvider`を登録した。
- fileとURLを既定アプリケーションで開く処理へ`UseShellExecute = true`を明示した。
- `MD5CryptoServiceProvider`と`SHA512Managed`を`MD5.Create()`、`SHA512.Create()`へ置き換えた。hashの種類と16進文字列形式は変更していない。
- hash計算で利用するstreamとalgorithmを確実に破棄するようにした。
- Form2のフォーム間受け渡し用propertyをWinForms designerのserialize対象外として明示した。
- 既存の`AssemblyInfo.cs`へWindows 7.0以降を示す`SupportedOSPlatform`を追加した。
- `Form1.resx`に以前から存在する完全同一の重複entryは、従来どおり後続entryを無視するため`MSB3568`だけを抑制した。resourceの内容自体は変更していない。

未使用の`System.Web`参照とBinaryFormatter関連の未使用`using`も削除した。BinaryFormatterによるobject deserializeは実装に存在せず、`.resx`内の該当文字列はschema説明だけである。

## buildとtest

既存scriptを引き続き利用できる。

```powershell
.\scripts\build-legacy.ps1
.\scripts\build-legacy.ps1 -Configuration Release
.\scripts\test-regression.ps1

dotnet restore .\Mid2BMS.sln -p:Platform=x86
dotnet build .\Mid2BMS.sln --no-restore -c Debug -p:Platform=x86
```

実行には.NET 10 Windows Desktop Runtimeが必要である。

## 検証結果

2026-09-19に次を確認した。

- Visual Studio 2026 / MSBuild 18.4: Debug x86 build成功
- Visual Studio 2026 / MSBuild 18.4: Release build成功
- .NET SDK 10.0.200: Debug x86 build成功
- Debug managed assembly: `32BITREQ=1`
- Release managed assembly: `32BITREQ=0`
- Debug apphost: x86
- Release apphost: x64
- `runtimeconfig.json`: `net10.0` / `Microsoft.WindowsDesktop.App 10.0.0`
- Debug WinForms apphost: startup後3秒間の継続動作を確認
- Golden Master: 7 fixture、91ファイル完全一致
- `culture.ini`、日本語resource、`_Docs`、`NVorbis.dll`の出力を確認

## Phase 4の境界と残件

Phase 4ではruntime移行に必要なcompatibility変更だけを行った。`Mid2BMS.Core`とWinForms UIの分離、class責務の整理、変換アルゴリズムの変更はPhase 5以降の対象とする。

`NVorbis.dll`は.NET Framework時代のbinaryをそのまま参照している。通常起動とGolden Masterは通過しているが、Golden MasterにはOGG読込経路が含まれないため、この経路は今後の依存更新時に専用testを追加する必要がある。

modern compilerが報告する既存code warningは残っている。今回のruntime移行に直接関係しないため、挙動変更を避けてPhase 4では修正していない。
