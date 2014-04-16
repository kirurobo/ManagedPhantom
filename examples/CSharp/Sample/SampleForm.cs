﻿/**
 * ------------------------------------------------
 * ManagedPhantom
 * 
 * Sample
 * 
 * Copyright (c) 2014 Kirurobo
 * http://twitter.com/kirurobo
 * 
 * This software is released under the MIT License.
 * http://opensource.org/licenses/mit-license.php
 * 
 * 
 * REQUIREMENTS
 *  - PHANTOM haptic device
 *  - PHANTOM Device Drivers
 *  - hd.dll (Sensable OpenHaptics Toolkit)
 * 
 * ------------------------------------------------
 */

using System;
using System.Windows.Forms;
using ManagedPhantom;

namespace Sample
{
    /// <summary>
    /// SimplePhantomの利用サンプル。
    /// OpenHaptics の「HelloHapticDevice」と同様です。
    /// </summary>
    public partial class SampleForm : Form
    {
        /// <summary>
        /// PHANTOMデバイス操作のインスタンス
        /// </summary>
        SimplePhantom Phantom;

        /// <summary>
        /// PHANTOM手先をこの座標に吸着させます
        /// </summary>
        SimplePhantom.Vector3D TargetPosition = new SimplePhantom.Vector3D(0.0, -20.0, 5.0);   // PHANTOM座標系の値。単位 [mm]

        /// <summary>
        /// PHANTOM手先座標をここに記憶
        /// </summary>
        SimplePhantom.Vector3D HandPosition;
        

        /// <summary>
        /// フォーム（ウィンドウ）のコンストラクタ
        /// </summary>
        public SampleForm()
        {
            // フォームの準備
            InitializeComponent();
        }

        /// <summary>
        /// フォームが最初に表示された時の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SampleForm_Load(object sender, EventArgs e)
        {
            // PHANTOMの準備
            Phantom = new SimplePhantom();     // デフォルトのPHANTOMデバイスに接続
            Phantom.AddSchedule(MainLogic);    // 1kHzで呼ばれるメソッドを指定
        }


        /// <summary>
        /// PHANTOMで呼ばれるメソッド。
        /// ここで発揮力を設定するなどの処理を行う。
        /// </summary>
        /// <returns>実行を繰り返したい場合はtrueを返す</returns>
        private bool MainLogic()
        {
            // ある一点に吸着するサンプルです

            // 定数
            const double stiffness = 0.05;              // バネ定数 k [N/mm]
            const double gravityWellInfluence = 30;     // 手先から吸着点までがこの距離未満なら吸着 [mm]

            // PHANTOM情報取得
            HandPosition = Phantom.GetPosition();               // PHANTOM手先座標 [mm]

            // 変数
            SimplePhantom.Vector3D vector = TargetPosition - HandPosition;    // 手先から吸着点に向かうベクトル [mm]
            SimplePhantom.Vector3D force;                                     // PHANTOMで発生させる力のベクトル [N]

            // 影響範囲内ならば距離に比例した力を与え、そうでなければ0とする
            if (vector.Length < gravityWellInfluence)
            {
                force = stiffness * vector; // F = kx [N]
            }
            else
            {
                force = SimplePhantom.Vector3D.Zero;      // F = 0 [N]
            }

            // PHANTOMの発揮力を設定
            Phantom.SetForce(force);

            // 終わる場合はfalse、繰り返す場合は true を返す
            return true;
        }


        /// <summary>
        /// スタートボタンを押された時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStart_Click(object sender, EventArgs e)
        {
            Phantom.Start();        // PHANTOMの処理を開始
            displayTimer.Start();   // 情報表示用タイマーを開始
        }

        /// <summary>
        /// ストップボタンを押された時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonStop_Click(object sender, EventArgs e)
        {
            displayTimer.Stop();    // 情報表示用タイマーを停止
            Phantom.Stop();         // PHANTOMの処理を停止
        }

        /// <summary>
        /// フォーム（ウィンドウ）が閉じられる時に呼ばれる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SampleForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Phantom.Close();        // PHANTOM の利用を終了
        }

        /// <summary>
        /// 手先座標を定期的に表示させるタイマー呼び出し
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void displayTimer_Tick(object sender, EventArgs e)
        {
            // MainLogic() の中ではテキストボックスに表示させられないので、別の間隔で表示を更新します

            positionTextBox.Text = HandPosition.ToString(); // 現在の手先座標を表示
        }
    }
}
