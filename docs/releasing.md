# リリース手順

## 配布物の作成

Windows上でリポジトリのルートから次を実行する。

```powershell
.\scripts\package-release.ps1 -Version v20260922
```

スクリプトは回帰テストを実行した後、Windows x64向けの自己完結版を`dotnet publish`し、README、リリースノート、MITライセンス、NVorbisのMs-PLライセンスを同梱する。生成先は次のとおり。

```text
artifacts/release/
 ├ Mid2BMS-v20260922-win-x64/
 ├ Mid2BMS-v20260922-win-x64.zip
 └ Mid2BMS-v20260922-win-x64.zip.sha256
```

自己完結版なので利用者による.NET Desktop Runtimeの追加インストールは不要。trimとsingle-file化は、legacy WinForms・resource・ファイル配置への影響を避けるため使用しない。

テスト済みの作業ツリーでパッケージだけ再生成する場合は`-SkipTests`を指定できる。

## バージョン更新

日付バージョンは表示用に`vYYYYMMDD`、Windowsの数値ファイルバージョンには`YYYY.M.D.0`を使う。次回リリースでは次の3箇所を同時に更新する。

1. `Directory.Build.props`の`Version`、`AssemblyVersion`、`FileVersion`、`InformationalVersion`
2. `docs/releases/vYYYYMMDD.md`
3. `README.md`のCurrent release

## 公開前確認

1. `git status`で意図しない変更がないことを確認する。
2. パッケージスクリプトをテスト省略なしで実行する。
3. 展開先の`Mid2BMS.exe`を起動する。
4. `.zip.sha256`でZIPのハッシュを照合する。
5. コミット後に`vYYYYMMDD`タグを作成し、ZIPとchecksumをリリースへ添付する。
