# LiveAlert

YouTube のライブ開始をポーリングで検知し、警告帯と音声/BGM で知らせるアプリです。Android 版は X（旧Twitter）の Space 通知検知にも対応し、Windows 版は YouTube 専用です。

# 主要ポイント
- YouTube: 公開ページのポーリング検知（公式 API 不使用）
- EVA 風の警告帯オーバーレイ + 音声/BGM


# 対応プラットフォーム
- Android: YouTube + X Space
- Windows (.NET8): YouTube のみ、ライブ録画機能あり

# Windows版特有の機能

- イースターエッグ機能
- ライブ録画機能

# Android版特有の機能

- X Spaceの通知を検知する機能

## かんたん設定（Android）
1) インストールして起動  
2) **権限設定** を開き、以下を有効化  
   - 通知  
   - 通知アクセス（X Space 用）  
   - 他のアプリの上に表示  
3) **常駐ON/OFF** を ON

#　より詳細なREADME

- アプリから「このプログラムについて」を表示してください。

# ライセンス
- MITライセンス

# 変更履歴

- 2026/01/29  andorid v0.11.0 初版公開
- 2026/02/01  andorid v0.12.0 色設定をカラーピッカーに変更
- 2026/02/01  andorid v0.12.0 背景色と文字色がきちんと反映されていない問題を修正（結果として同じconfigで見た目が変わります）
- 2026/02/01  andorid v0.12.0 設定のエクスポート、インポート機能を実装
- 2026/02/02  andorid v0.13.0 メン限のライブが拾えないケースがあったので検知方法を改善
- 2026/03/22  andorid v0.13.1 Already notified ログを抑制
- 2026/03/22  andorid v0.13.2 監視bugfixを念のためAndroid版にも波及させておく

- 2026/03/08  Windows v0.1    初版公開　タスクトレイ常駐 / YouTube 専用
- 2026/03/15  Windows v0.2.0  イースターエッグを追加
- 2026/03/22  Windows v0.2.1  Already notified ログを抑制、謎の常駐停止対処
- 2026/03/28  Windows v0.3.0  ライブ録画機能を追加、謎の常駐停止の原因がわかったのでbugfix
  
