# Windows版の録画設定とトラブル対処

## インストール・更新

1. [Windows版リリース](https://github.com/yasagurebug/LiveAlert/releases)から `LiveAlert-Windows-Setup-<バージョン>.exe` をダウンロードして実行します。更新時は、録画が終わってからタスクトレイの「終了」でLiveAlertを終了してください。
2. 録画を使う場合は、以下の外部ツールをPATHの通った場所に配置します。インストーラには含まれていません。通知だけを使う場合は不要です。
   - [yt-dlp](https://github.com/yt-dlp/yt-dlp#installation)：YouTubeから動画を取得します。
   - [ffmpeg・ffprobe](https://ffmpeg.org/download.html#build-windows)：映像・音声の結合と完成ファイルの確認に使います。Windows向け配布物から両方の実行ファイルを配置してください。
   - YouTubeのJavaScript処理に必要なランタイムなどは、[yt-dlpの公式案内](https://github.com/yt-dlp/yt-dlp/wiki/EJS)に従って準備します。
3. 新しく開いたターミナルで、次を実行できることを確認します。

```bash
yt-dlp --version
ffmpeg -version
ffprobe -version
```

4. LiveAlertを起動し、設定で「ライブ録画を有効にする」をONにして録画専用フォルダを指定します。指定日数を超えたファイルは自動削除されるため、他のファイルを混在させないでください。

通常はここまでで設定完了です。bgutilの補修は全員に必要な作業ではありません。

### 0.3.6以降で改善されたこと

LiveAlertは、yt-dlpが異常終了しても、取得済みの映像・音声を回収して完成MP4の作成を試みます。映像・音声と再生時間を確認してから一時ファイルを削除します。取得自体の失敗や、欠落データの再取得を保証する機能ではありません。

## 録画に失敗した場合

録画先にある対象配信の `*_ytdlp.log` と `*_ffmpeg.log` を確認します。アプリ全体のログは、タスクトレイの「設定フォルダを開く」から参照できる `livealert.log` です。

| ログ・症状 | 次に確認すること |
| --- | --- |
| `ffprobe` や `ffmpeg` を起動できない | 上記のバージョン確認。PATHを変更した場合はLiveAlertも再起動する |
| メンバー限定・視聴権限のエラー | その配信を視聴できる権限と、必要なら `%APPDATA%\LiveAlert\cookies.txt` の設定。bgutil補修では権限は付与されない |
| `HTTP Error 503` のみ | 一時的な取得失敗の場合があるため、後続ログと完成MP4の有無を確認する。これだけではbgutil補修の対象にしない |
| `generate_once.ts` または `generate_once.js` の `--version` が `timed out after 15.0 seconds` | 下記のbgutil補修の対象を確認する |
| 別のエラー・原因不明 | [問題報告](https://github.com/yasagurebug/LiveAlert/issues)にアプリの版、外部ツールの版、エラー文を記載する |

Cookie、POトークン、署名付きURLを含むログ全文は公開しないでください。失敗した録画の残存ファイルは、調査・復旧が終わるまで手動で削除しないでください。

## bgutilとは

[bgutil-ytdlp-pot-provider](https://github.com/Brainicism/bgutil-ytdlp-pot-provider)は、YouTubeが一部の通信に要求する「POトークン」を生成する、yt-dlp用の追加プラグインです。LiveAlert本体やインストーラには含まれていません。必要性・導入方法は[yt-dlpのPOトークン案内](https://github.com/yt-dlp/yt-dlp/wiki/PO-Token-Guide)で確認できます。

bgutilを導入していない人、または上記のバージョン確認タイムアウトが発生していない人は、以下の補修を行う必要はありません。このエラー対策のためだけにbgutilを新規導入する必要もありません。

## bgutilのバージョン確認タイムアウトを補修する

この補修は、バージョン確認のタイムアウトを警告として扱い、利用可能な次のプロバイダーを試せるようにします。トークン生成の成功を保証したり、タイムアウト時間を延ばしたりするものではありません。次のプロバイダーも使えない場合は、bgutilの公式手順で環境を確認してください。

### 1. 補修ファイルを入手する

[Windows 0.3.7のリリース](https://github.com/yasagurebug/LiveAlert/releases/tag/0.3.7_windows)にある `LiveAlert-Windows-Recording-Help-0.3.7.zip` を展開します。中にはこの説明と `scripts/fix_bgutil_timeout.ps1` が入っています。リポジトリを取得済みなら、その `scripts` フォルダのファイルも利用できます。

録画を終了し、タスクトレイからLiveAlertを終了してから操作してください。

### 2. 補修対象のファイルを確認する

以下は **PowerShell** で実行します。展開したフォルダ（`scripts` フォルダが見える場所）でPowerShellを開き、LiveAlertと同じWindowsアカウントで、使われているyt-dlpの場所を確認します。

```powershell
$ytDlpPath = (Get-Command yt-dlp -ErrorAction Stop).Source
$ytDlpPath
```

pipまたはpipxで導入され、`Scripts\yt-dlp.exe` に配置されている環境では、次で候補を確認できます。Pythonを新たにインストールする必要はありません。

```powershell
$pythonRoot = Split-Path (Split-Path $ytDlpPath -Parent) -Parent
$modulePath = Join-Path $pythonRoot 'Lib\site-packages\yt_dlp_plugins\extractor\getpot_bgutil_script.py'
Test-Path -LiteralPath $modulePath
$modulePath
```

`True` と対象ファイルのパスが表示され、実際に使うyt-dlpの環境であることを確認してから進みます。yt-dlpを複数導入している場合は、PATH順序も確認してください。

`False` の場合は、この方法では対象を特定できていません。単体exe版、ZIP形式のプラグイン、独自の配置などでは場所が異なります。無関係なPython環境へ適用せず、[bgutilの導入手順](https://github.com/Brainicism/bgutil-ytdlp-pot-provider#readme)で導入方法を確認するか、使用している導入方法とエラー文を[問題報告](https://github.com/yasagurebug/LiveAlert/issues)に記載してください。この補修スクリプトはZIP内・exe内のモジュールは変更しません。

### 3. 適用する

同じPowerShellで、上記の `$modulePath` を設定した状態で実行します。

```powershell
.\scripts\fix_bgutil_timeout.ps1 -ModulePath $modulePath
```

ダウンロードしたファイルがブロックされる場合は、内容を確認したうえで次を実行し、上のコマンドを再実行します。

```powershell
Unblock-File -LiteralPath .\scripts\fix_bgutil_timeout.ps1
```

実行ポリシーで引き続き拒否される場合は、その環境の管理方針を確認してください。ポリシーを回避・無効化する操作はこの手順には含みません。対象モジュールへの書き込み権限がない場合も自動では変更しません。

| 表示 | 結果 |
| --- | --- |
| `Applied bgutil timeout fix: ...` | 適用完了 |
| `bgutil timeout fix is already applied.` | 適用済み。変更なし |
| `Unsupported bgutil source; no changes were made.` | 対応するコードと一致しないため変更なし。手動で置換せず、上流で修正済みか、別の版かを確認する |

### 4. 動作を確認する

LiveAlertを再起動し、次の録画で完成MP4に音声があること、途中へ移動できることを確認します。再び15秒タイムアウトした場合でも `provider version check timed out; trying the next available provider` という警告に変わり、利用可能な次候補があれば処理を継続します。タイムアウトが発生しなかった録画では、この警告は出ません。

別のエラーで失敗する場合は、この補修を繰り返さず、そのエラーを切り分けます。

### 5. 元に戻す場合

録画とLiveAlertを終了し、同じ対象パスを指定して実行します。

```powershell
.\scripts\fix_bgutil_timeout.ps1 -ModulePath $modulePath -Restore
```

`Restored bgutil timeout check: ...` と表示されれば、このスクリプトが加えたタイムアウト捕捉部分だけを元に戻しています。設定・録画データ・パッケージのバージョンは変更しません。`bgutil timeout fix is not applied; no changes made.` なら補修は入っていません。対象コードが変わっていれば書き換えず停止します。

bgutilを更新・再インストールすると補修が置き換わる場合があります。上流版で修正されたか確認し、同じ症状が残る場合だけ対応してください。
