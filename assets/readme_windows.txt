-----------------------------------------------------------
　　LiveAlert (Windows)
-----------------------------------------------------------

LiveAlert は、YouTube ライブ配信の開始をポーリングで監視し、
警告帯と音声/BGM で知らせる Windows 向けのアプリです。
- YouTube ライブ配信の開始をポーリング検知（公式 API 不使用）

■　特徴
- EVA 風の警告帯表示
- 音声/BGM の同時再生
- タスクトレイ常駐で監視継続

■　起動と常駐
- 起動すると設定画面が開き、同時にタスクトレイへ常駐します。
- タスクトレイ側が本体で、設定画面を閉じても監視は継続します。
- 常駐中は常に YouTube を監視します。監視開始 / 監視停止の切り替えはありません。
- 完全に終了したい場合は、タスクトレイメニューから「終了」を選んでください。

■　設定方法

1) 全体設定
- ポーリング間隔（秒）：YouTube を監視する間隔
- 最大鳴動時間（秒）：鳴らし続ける最大時間
- ループ間隔（秒）：音声/BGM をループ再生する際の待ち時間
- 帯位置：top / center / bottom
- 帯高さ（px）：帯の高さ
- Windows起動時に自動起動する：ログオン時に自動で常駐開始します
- ライブ録画を有効にする：ライブ時に録画を行います（詳細は後述）

2) 監視対象設定
- 設定できる項目：
  - YouTube URL
    - チャンネル URL または watch URL を入力
      例: https://www.youtube.com/@xxxxx / https://www.youtube.com/channel/XXXX / https://www.youtube.com/watch?v=YYYY
  - 表示名
  - メッセージ（{label} は表示名に置換）
  - 音声ファイル / BGM ファイル
  - 音声の音量 / BGM の音量（スライダー）
  - 背景色 / 文字色（Windows のカラーピッカーで選択）
  - 追加 / 削除

3) 帯と音声の動作
- Windows 版では帯表示と音声再生を固定動作とします。
- 非ロック時は、帯表示と音声再生を行います。
- ロック中は、帯は表示せず、音声のみ継続します。
- 停止する場合は、タスクトレイメニューの「アラーム停止」を使います。
- 表示名が `SAMPLE` のアラートは、帯をクリックしても YouTube を開きません。

4) タスクトレイメニュー
- 設定を開く：設定画面を表示します
- アラーム停止：現在のアラートを停止します
- テスト発報：今の設定でアラートをテストします
- 設定フォルダを開く：config.json の保存先を開きます
- このプログラムについて：Windows 版の説明を表示します
- 外部ライセンス：同梱フォント/音源のライセンスを確認できます
- 終了：常駐と監視を終了します

5) 起動時の動作
- `Windows起動時に自動起動する` が OFF の場合、exe 起動時に設定画面を表示します。
- `Windows起動時に自動起動する` が ON の場合、exe 起動時は設定画面を出さず、そのままタスクトレイへ常駐します。
- 設定画面を開きたい場合は、タスクトレイをダブルクリックするか、右クリックメニューの「設定を開く」を使います。

6) イースターエッグ
- labelが特定の文字列の時に発生するイベントがあります。

7) ライブ録画機能
- ライブ開始時に、yt-dlpとffmpegを使って録画を行います。
- yt-dlpとffmpegがパスの通った場所に置かれている必要があります。
- yt-dlp https://github.com/yt-dlp/yt-dlp/releases
- ffmpeg https://ffmpeg.org/download.html#build-windows
- 録画先のフォルダは、！！！30日経過したファイルを自動削除！！！します。
- そのため、他のファイルが混ざらないフォルダを指定してください。
- 録画終了または停止時に、tsファイルはmp4に変換されます。
- yt-dlpはyoutubeの仕様変更に合わせ頻繁にアップデートされますので、定期的に yt-dlp -U を行ってアップデートを行うのをお勧めします。
- 保存日数は 設定ファイル（JSON）の recordingRetentionDays で変更できます（LiveAlertの再起動が必要です）

■　設定ファイル
- 保存先: %APPDATA%\LiveAlert\config.json
- 設定内容は画面上で変更すると自動保存されます。

■　補足
- 監視対象は YouTube のみです。X Space は Windows 版では非対応です。
- タスクトレイのツールチップに監視状態が表示されます。
- 設定画面を閉じても終了しません。
- 実行中は音声ファイルや BGM ファイルのパスが存在することを確認してください。

■　ライセンス
- このプログラムのライセンスは MIT ライセンスとします。
- 同梱フォント・音源・画像のライセンスはアプリ内の「外部ライセンス」から確認できます。

MIT License

Copyright (c) 2026 yasagurebug

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
