/**
 * ------------------------------------------------
 * ManagedPhantom
 * 
 * Simple PHANToM
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
using System.Collections.Generic;
using ManagedPhantom.Structs;

namespace ManagedPhantom
{
    /// <summary>
    /// 簡易的にPHANTOMを扱うためのクラスです
    /// </summary>
    public class SimplePhantom
    {
        uint hHD = (uint)Hd.DeviceHandle.HD_INVALID_HANDLE;     // デバイスハンドル

        List<Hd.SchedulerCallback> CallbackMethods;     // 参照が無くなるとGCされるので、メソッドを保持
        List<ulong> ScheduleHandles;                     // HDAPIDでスケジューリングした際のハンドルを保持
        private Buttons CurrentButtons = Buttons.None;	// 現在のPHANTOMボタン押下状況
        private Buttons LastButtons = Buttons.None;	    // 前回Update時のPHANTOMボタン
        

        /// <summary>
        /// 可動範囲下限 [mm]
        /// </summary>
        public Vector3D WorkspaceMinimum { get; private set; }

        /// <summary>
        /// 可動範囲上限 [mm]
        /// </summary>
        public Vector3D WorkspaceMaximum { get; private set; }

        /// <summary>
        /// 推奨可動範囲下限 [mm]
        /// </summary>
        public Vector3D UsableWorkspaceMinimum { get; private set; }

        /// <summary>
        /// 推奨可動範囲上限 [mm]
        /// </summary>
        public Vector3D UsableWorkspaceMaximum { get; private set; }

        /// <summary>
        /// 机の面に相当するY座標 [mm]
        /// </summary>
        public double TableTopOffset { get; private set; }

        /// <summary>
        /// ジンバル部を基準としたペン先端の座標 [mm] (PHANTOM座標系)
        /// </summary>
        public Vector3D TipOffset = new Vector3D(0.0, 0.0, -40.0);

        /// <summary>
        /// PHANTOMの処理が実行中なら true とする
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 非同期で呼ばれるメソッドです
        /// </summary>
        /// <returns>true:要継続, false:終了</returns>
        public delegate bool Callback();


        /// <summary>
        /// デフォルトのデバイスに接続します
        /// </summary>
        public SimplePhantom()
        {
            IsRunning = false;

            // デフォルトのデバイスを準備
            hHD = Hd.hdInitDevice(Hd.DeviceHandle.HD_DEFAULT_DEVICE);
            ErrorCheck();

            // コールバックメソッドを保持するリスト
            CallbackMethods = new List<Hd.SchedulerCallback>();

            // スケジューリングされたメソッドのハンドルをこれで保持
            ScheduleHandles = new List<ulong>();

            // 可動範囲を取得
            LoadWorkspaceLimit();
        }

        /// <summary>
        /// デバイスの使用を終了し、切断します
        /// </summary>
        public void Close()
        {
            Stop();
            ClearSchedule();

            if (hHD != (uint)Hd.DeviceHandle.HD_INVALID_HANDLE)
            {
                Hd.hdDisableDevice(hHD);
                ErrorCheck("Disable device");
            }
        }

        #region スケジューリング関連
        /// <summary>
        /// 非同期処理を開始します
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            // 力を発生させるのは標準でON
            Hd.hdEnable(Hd.Capability.HD_FORCE_OUTPUT);
            ErrorCheck("Enable force output");

            // 非同期処理も開始
            Hd.hdStartScheduler();
            ErrorCheck("Start scheduler");

            IsRunning = true;
        }

        /// <summary>
        /// 非同期処理を停止します
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;

            Hd.hdStopScheduler();
            ErrorCheck("StopScheduler");

            // 力も停止
            Hd.hdDisable(Hd.Capability.HD_FORCE_OUTPUT);
            ErrorCheck("Disable force output");
        }

        /// <summary>
        /// 同期的に処理を呼び出します
        /// </summary>
        public void Do(Callback callback)
        {
            Hd.hdScheduleSynchronous(
                (data) => { return DoCallback(callback); },
                IntPtr.Zero,
                Hd.Priority.HD_DEFAULT_SCHEDULER_PRIORITY
                );
            ErrorCheck("ScheduleSynchronous");
        }

        /// <summary>
        /// 非同期実行にメソッドを追加します
        /// </summary>
        /// <param name="callback">要継続ならtrueを返すコールバックメソッド</param>
        public void AddSchedule(Callback callback)
        {
            Hd.SchedulerCallback method = (data) =>
                {
                    return DoCallback(callback);
                };
            CallbackMethods.Add(method);

            ulong handle = Hd.hdScheduleAsynchronous(
                method,
                IntPtr.Zero,
                Hd.Priority.HD_DEFAULT_SCHEDULER_PRIORITY
                );
			ErrorCheck("ScheduleAsynchronous");
            ScheduleHandles.Add(handle);
        }

        /// <summary>
        /// コールバックメソッド呼び出しをより簡略化するためのラップ
        /// </summary>
        /// <param name="callback">引数無しでboolを返すだけに簡略化したメソッド</param>
        /// <returns>完了したか</returns>
        private Hd.CallbackResult DoCallback(Callback callback)
        {
            bool result;

            Hd.hdBeginFrame(hHD);
            result = callback();
            Hd.hdEndFrame(hHD);

            return (result ? Hd.CallbackResult.HD_CALLBACK_CONTINUE : Hd.CallbackResult.HD_CALLBACK_DONE);
        }

        /// <summary>
        /// 登録済の非同期実行処理を全て消去します
        /// </summary>
        public void ClearSchedule()
        {
            foreach (uint handle in ScheduleHandles)
            {
                Hd.hdUnschedule(handle);
                ErrorCheck("Unschedule #" + handle.ToString());
            }
            ScheduleHandles.Clear();
            CallbackMethods.Clear();
        }

        /// <summary>
        /// 処理が1秒間に何回呼ばれるかを指定します
        /// </summary>
        /// <param name="rate">周期 [Hz] 500 or 1000</param>
        public void SetSchedulerRate(uint rate)
        {
            Hd.hdSetSchedulerRate(rate);
            ErrorCheck();
        }

        #endregion

        #region 情報取得メソッド
        /// <summary>
        /// 現在のPHANTOM手先座標を返します
        /// </summary>
        /// <returns>位置ベクトル [mm]</returns>
        public Vector3D GetPosition()
        {
            double[] position = new double[3] { 0, 0, 0 };
            Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_POSITION, position);
            return new Vector3D(position);
        }

        /// <summary>
        /// ペン先端の座標を返します
        /// </summary>
        /// <returns>ペン先端座標 [mm]</returns>
        public Vector3D GetTipPosition()
        {
            double[] position = new double[3] { 0, 0, 0 };
            double[] matrix = new double[16];
            Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_POSITION, position);
            Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_TRANSFORM, matrix);

            return new Vector3D(
                (position[0] + matrix[0] * TipOffset.X + matrix[4] * TipOffset.Y + matrix[8] * TipOffset.Z),
                (position[1] + matrix[1] * TipOffset.X + matrix[5] * TipOffset.Y + matrix[9] * TipOffset.Z),
                (position[2] + matrix[2] * TipOffset.X + matrix[6] * TipOffset.Y + matrix[10] * TipOffset.Z)
                );
        }

        /// <summary>
        /// 現在のPHANTOM手先速度を返します
        /// </summary>
        /// <returns>速度ベクトル [mm/s]</returns>
        public Vector3D GetVelocity()
        {
            double[] velocity = new double[3] { 0, 0, 0 };
            Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_VELOCITY, velocity);
            return new Vector3D(velocity);
        }

        /// <summary>
        /// 現在のPHANTOMジンバル姿勢を返します
        /// <remarks>手先の姿勢ではありません。ジンバル部エンコーダの値です。</remarks>
        /// </summary>
        /// <returns>ジンバル部分の内、根元からペン部にかけて X～Z に対応した角度 [rad]</returns>
        public Vector3D GetGimbalAngles()
        {
            double[] gimbals = new double[3] { 0, 0, 0 };
            Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_GIMBAL_ANGLES, gimbals);
            return new Vector3D(gimbals);
        }

        /// <summary>
        /// 現在のPHANTOM手先変換行列を返します
        /// </summary>
        /// <returns>4 × 4 変換行列</returns>
        public Matrix GetTransformMatrix()
        {
            double[] value = new double[16];
            Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_TRANSFORM, value);
            return new Matrix(value);
        }

        /// <summary>
        /// 現在押されているボタンを取得します
        /// </summary>
        /// <returns>Button1 | Button2 | Button3 | Button4</returns>
        public Buttons GetButton()
        {
            int[] button = new int[1];
            Hd.hdGetIntegerv(Hd.ParameterName.HD_CURRENT_BUTTONS, button);
            return (Buttons)button[0];
        }

        /// <summary>
        /// PHANTOのボタン押下状況を更新
        /// GetButtonDown(), GetButtonUp() を利用する際はそのタイミングでこれを呼ぶこと。
        /// </summary>
        /// <returns>The buttons.</returns>
        public Buttons UpdateButtons()
        {
            LastButtons = CurrentButtons;
            CurrentButtons = GetButton();
            return CurrentButtons;
        }

        /// <summary>
        /// 指定されたボタンがまさに押されたところならばtrueを返す
        /// </summary>
        /// <returns><c>true</c>, if button was down, <c>false</c> otherwise.</returns>
        /// <param name="button">Button.</param>
        public bool GetButtonDown(Buttons button)
        {
            return (((LastButtons & button) == Buttons.None) && ((CurrentButtons & button) == button));
        }

        /// <summary>
        /// 指定されたボタンがまさに離されたところならばtrueを返す
        /// </summary>
        /// <returns><c>true</c>, if button was up, <c>false</c> otherwise.</returns>
        /// <param name="button">Button.</param>
        public bool GetButtonUp(Buttons button)
        {
            return (((LastButtons & button) == button) && ((CurrentButtons & button) == Buttons.None));
        }

        /// <summary>
        /// サーボループ開始からの経過時間を取得します
        /// </summary>
        /// <returns>時間 [s]</returns>
        public double GetSchedulerTimeStamp()
        {
            return Hd.hdGetSchedulerTimeStamp();
        }

        #endregion

        #region 情報設定メソッド
        /// <summary>
        /// PHANTOM発揮力を設定します
        /// </summary>
        /// <param name="force">力のベクトル [N]</param>
        public void SetForce(Vector3D force)
        {
            double[] forceArray = force.ToArray();
            Hd.hdSetDoublev(Hd.ParameterName.HD_CURRENT_FORCE, forceArray);
        }

        #endregion

        #region 内部メソッド
        /// <summary>
        /// 可動範囲を取得
        /// </summary>
        private void LoadWorkspaceLimit()
        {
            double[] val = new double[6];

            // 可動限界範囲を取得
            Hd.hdGetDoublev(Hd.ParameterName.HD_MAX_WORKSPACE_DIMENSIONS, val);
            ErrorCheck("Getting max workspace");
            WorkspaceMinimum = new Vector3D(val[0], val[1], val[2]);
            WorkspaceMaximum = new Vector3D(val[3], val[4], val[5]);

            // 推奨可動範囲を取得
            Hd.hdGetDoublev(Hd.ParameterName.HD_USABLE_WORKSPACE_DIMENSIONS, val);
            ErrorCheck("Getting usable workspace");
            UsableWorkspaceMinimum = new Vector3D(val[0], val[1], val[2]);
            UsableWorkspaceMaximum = new Vector3D(val[3], val[4], val[5]);

            // 机の高さを取得
            float[] offset = new float[1];
            Hd.hdGetFloatv(Hd.ParameterName.HD_TABLETOP_OFFSET, offset);
            ErrorCheck("Getting table-top offset");
            TableTopOffset = (double)offset[0];
        }

        /// <summary>
        /// 直前のHDAPI呼び出しでエラーがあれば、例外を発生させます
        /// </summary>
        static private void ErrorCheck()
        {
           ErrorCheck("");
        }

        /// <summary>
        /// 直前のHDAPI呼び出しでエラーがあれば、例外を発生させます
        /// </summary>
        /// <param name="situation">何をしていたかを伝える文字列</param>
        static private void ErrorCheck(string situation)
        {
            Hd.ErrorInfo error;

            if (Hd.IsError(error = Hd.hdGetError()))
            {
                string message = Hd.GetErrorString(error.ErrorCode);

                if (situation.Equals(""))
                {
                    System.Diagnostics.Debug.WriteLine("HDAPI error : " + message);
                    throw new HdApiException(message);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("HDAPI error : " + situation + " / " + message);
                    throw new HdApiException(situation + " / " + message);
                }
            }
        }
        #endregion

    }
}
