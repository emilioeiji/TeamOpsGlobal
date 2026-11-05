# 配布とデプロイ

## 目的

このドキュメントは TeamOps の配布前確認、設定、公開後確認をまとめます。

## 配布前チェック

- Solution がビルドできること。
- `App.config` の `DatabasePath` が正しいこと。
- SQLite の `DatabasePath` がローカルディスクを指していること。
- `StorageRoot` が `\\server\share\TeamOps` 形式の UNC パスであること。マップドドライブは使用しません。
- PR/CL テンプレートと出力フォルダーが存在すること。
- Hikitsugui 添付フォルダーにアクセスできること。
- Haidai エクスポート先に書き込みできること。
- 製造 TXT/DAT/EC2 フォルダーにアクセスできること。
- WebView2 Runtime が対象 PC にあること。
- migration と seed が適用済みであること。

## 配布手順

1. 対象ブランチを最新化します。
2. Release 構成でビルドします。
3. 必要な `ui/` assets、DLL、設定ファイル、テンプレート参照が含まれているか確認します。
4. 配布先へコピーします。
5. `App.config` と接続先 DB を確認します。
6. 起動、ログイン、Dashboard 表示を確認します。

配布前に次を実行し、`FAIL` がないことを確認します。

```powershell
.\scripts\Test-TeamOpsDeployment.ps1 -AppDirectory .\artifacts\TeamOps.UI -TestWrite
```

本番設定例は `docs/examples/TeamOps.UI.production.config` と `docs/examples/TeamOps.OperatorApp.production.config` です。

## ProductionMonitorProbe

製造モニター関連を公開する場合は Probe も同じ設定で配布します。Probe の config が UI と同じ DB を指しているか確認してください。

## 公開後確認

- ログインできること。
- Dashboard が白画面にならないこと。
- Operadores、Relatorios、Administracao など代表画面が開くこと。
- 製造インポートが必要な環境では `schema-check` と `production-diagnostics` を実行できること。
- System Log に重大なエラーが出ていないこと。

## ロールバック

問題が出た場合は、すべての TeamOps を終了し、直前のバージョン別配布フォルダーに戻します。SQLite を復元する場合は、必ず事前バックアップと展開担当者の確認を使用します。
