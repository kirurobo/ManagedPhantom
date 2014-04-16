ManagedPhantom

Copyright (c) 2013-2014 Kirurobo
Released under the MIT license
http://opensource.org/licenses/mit-license.php


■ 概要
・Sensable (GeoMagic社) PHANTOM を .NET Framework から使うためのラッパーです。
・OpenHaptics Toolkit の、HD API のみ対応しています。


■ 内容物
・ManagedPhantom.sln … クラスライブラリとC#サンプルをまとめたソリューションです。
・ManagedPhantom … クラスライブラリ部分
・examples/CSharp … C#での利用サンプル
・examples/Unity … Unityでの利用サンプル ※実行すると終了できなくなる問題があります


■ 必要環境
・PHANTOM シリーズ（PHANTOM Omni でのみ動作確認）
　ドライバーもインストール済で、動作すること。

・OpenHaptics Toolkit 3.0 または OpenHaptics Toolkit Acacemic Edition 3.0
　　hd.dll が必要です。
　　プラグインに入れる必要はないため、Unity Pro でなくとも動作するはずです。
　　ただし環境によっては Unity のプロジェクトフォルダ（Assetsより親のフォルダ）に
　　hd.dll を置く必要があるかも知れません。（ビルド済の場合は exe と同じフォルダ）


■ クラスライブラリ部分の説明
・Hd.cs
　HD API 相当の分ですが、基本的には直接こちらを扱わず、SimplePhantom を利用することを想定しています。
　HD API の仕様については、OpenHaptics Toolkit のマニュアルをご覧ください。
　全てテストした訳ではありません。

・SimplePhantom.cs
　HD API は隠蔽し、なるべく簡潔に利用できるようにしたもの。
　仕様はたぶん替わります。
　使用方法はサンプルをご覧ください。（不親切w）



■ 更新履歴
2014/04/16	公開用初版


■ 連絡先・配布元
@kirurobo
http://twitter.com/kirurobo
http://github.com/kirurobo
