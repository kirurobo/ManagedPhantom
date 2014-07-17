/**
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
using ManagedPhantom.Structs;

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
        Vector3D TargetPosition = new Vector3D(0.0, -20.0, 5.0);   // PHANTOM座標系の値。単位 [mm]

        /// <summary>
        /// PHANTOM手先座標 [mm]
        /// </summary>
        Vector3D HandPosition;
        
        /// <summary>
        /// PHANTOM手先速度 [mm/s]
        /// </summary>
        Vector3D HandVelocity;

        /// <summary>
        /// 剛体の仮想球体
        /// </summary>
        ManagedPhantom.RigidPrimitives.Orb[] RigidOrbs;

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
            Phantom.AddSchedule(ServoLoop);    // 1kHzで呼ばれるメソッドを指定

            // 球体を1つ作成
            RigidOrbs = new ManagedPhantom.RigidPrimitives.Orb[1];
            RigidOrbs[0] = new ManagedPhantom.RigidPrimitives.Orb(
                new Vector3D(0.0, 0.0, 0.0),    // 球体の中心座標 [mm]
                30.0                            // 球体の半径 [mm]
                );
        }


        /// <summary>
        /// PHANTOMで呼ばれるメソッド。
        /// ここで発揮力を設定するなどの処理を行う。
        /// </summary>
        /// <returns>実行を繰り返したい場合はtrueを返す</returns>
        private bool ServoLoop()
        {
            // 現在のPHANTOM情報
            HandPosition = Phantom.GetPosition();   // ジンバル部座標 [mm]
            HandVelocity = Phantom.GetVelocity();   // ジンバル部速度 [mm/s]

            // PHANTOMで発生させる力ベクトル [N]
            Vector3D force = Vector3D.Zero;

            // 剛体の球体を表現
            foreach (ManagedPhantom.RigidPrimitives.Orb orb in RigidOrbs)
            {
                force += orb.CalculateForce(HandPosition);
            }

            // PHANTOMの発揮力を設定
            Phantom.SetForce(force);

            // 終わる場合はfalse、繰り返す場合は true を返す
            return true;
        }


        #region GUI events

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
            // ServoLoop() の中ではテキストボックスに表示できないため、別の間隔で表示を更新します

            positionTextBox.Text = HandPosition.ToString(); // 現在の手先座標を表示
        }

        #endregion

    }
}
