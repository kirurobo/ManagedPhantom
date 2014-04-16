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

namespace ManagedPhantom
{
    /// <summary>
    /// 簡易的にPHANTOMを扱うためのクラスです
    /// </summary>
    public class SimplePhantom
    {
        uint hHD = (uint)Hd.DeviceHandle.HD_INVALID_HANDLE;     // デバイスハンドル
        Hd.ErrorInfo LastError;                                 // 最後に発生したエラー（HD_SUCCESSは対象外）

        List<Hd.SchedulerCallback> CallbackMethods;     // 参照が無くなるとGCされるので、メソッドを保持
        List<uint> ScheduleHandles;                     // HDAPIDでスケジューリングした際のハンドルを保持

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
            ScheduleHandles = new List<uint>();

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
                ErrorCheck();
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
            ErrorCheck();

            // 非同期処理も開始
            Hd.hdStartScheduler();
            ErrorCheck();

            IsRunning = true;
        }

        /// <summary>
        /// 非同期処理を停止します
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;

            Hd.hdStopScheduler();
            ErrorCheck();

            // 力も停止
            Hd.hdDisable(Hd.Capability.HD_FORCE_OUTPUT);
            ErrorCheck();

            IsRunning = false;
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
            ErrorCheck();
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

            uint handle = Hd.hdScheduleAsynchronous(
                method,
                IntPtr.Zero,
                Hd.Priority.HD_DEFAULT_SCHEDULER_PRIORITY
                );
            ErrorCheck();

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
                ErrorCheck();
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
            ErrorCheck();
            WorkspaceMinimum = new Vector3D(val[0], val[1], val[2]);
            WorkspaceMaximum = new Vector3D(val[3], val[4], val[5]);

            // 推奨可動範囲を取得
            Hd.hdGetDoublev(Hd.ParameterName.HD_USABLE_WORKSPACE_DIMENSIONS, val);
            ErrorCheck();
            UsableWorkspaceMinimum = new Vector3D(val[0], val[1], val[2]);
            UsableWorkspaceMaximum = new Vector3D(val[3], val[4], val[5]);

            // 机の高さを取得
            float[] offset = new float[1];
            Hd.hdGetFloatv(Hd.ParameterName.HD_TABLETOP_OFFSET, offset);
            ErrorCheck();
            TableTopOffset = (double)offset[0];
        }

        /// <summary>
        /// 直前のHDAPI呼び出しでエラーがあれば、例外を発生させます
        /// </summary>
        private void ErrorCheck()
        {
            Hd.ErrorInfo error;

            if (Hd.IsError(error = Hd.hdGetError()))
            {
                LastError = error;
                string message = Hd.GetErrorString(error.ErrorCode);

                System.Diagnostics.Debug.WriteLine("HDAPI error : " + message);
                throw new HdApiException(message);
            }
        }
        #endregion



        #region ベクトル、行列構造体
        /// <summary>
        /// 3次元ベクトル
        /// </summary>
        public struct Vector3D
        {
            /// <summary>
            /// X成分または第1成分の値
            /// </summary>
            public double X;

            /// <summary>
            /// Y成分または第2成分の値
            /// </summary>
            public double Y;

            /// <summary>
            /// Z成分または第3成分の値
            /// </summary>
            public double Z;

            /// <summary>
            /// 3成分からベクトルを作成します
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="z"></param>
            public Vector3D(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            /// <summary>
            /// 配列を元にベクトルを作成します
            /// </summary>
            /// <param name="value"></param>
            public Vector3D(double[] value)
            {
                X = value[0];
                Y = value[1];
                Z = value[2];
            }

            /// <summary>
            /// ベクトル1とベクトル2を加算した結果を返します
            /// </summary>
            /// <param name="v1">ベクトル1</param>
            /// <param name="v2">ベクトル2</param>
            /// <returns>2つのベクトルの和</returns>
            public static Vector3D operator +(Vector3D v1, Vector3D v2)
            {
                return new Vector3D(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
            }

            /// <summary>
            /// ベクトル1からベクトル2を引いた結果を返します
            /// </summary>
            /// <param name="v1">ベクトル1</param>
            /// <param name="v2">ベクトル2</param>
            /// <returns>2つのベクトルの差</returns>
            public static Vector3D operator -(Vector3D v1, Vector3D v2)
            {
                return new Vector3D(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
            }

            /// <summary>
            /// ベクトルを定数倍します
            /// </summary>
            /// <param name="vec">元のベクトル</param>
            /// <param name="scale">係数</param>
            /// <returns>スケーリング後のベクトル</returns>
            public static Vector3D operator *(Vector3D vec, double scale)
            {
                return new Vector3D(vec.X * scale, vec.Y * scale, vec.Z * scale);
            }

            /// <summary>
            /// ベクトルを定数倍します
            /// </summary>
            /// <param name="scale">係数</param>
            /// <param name="vec">元のベクトル</param>
            /// <returns>スケーリング後のベクトル</returns>
            public static Vector3D operator *(double scale, Vector3D vec)
            {
                return new Vector3D(vec.X * scale, vec.Y * scale, vec.Z * scale);
            }

            /// <summary>
            /// ベクトルに定数の逆数をかけます
            /// </summary>
            /// <param name="vec">元のベクトル</param>
            /// <param name="scale">係数</param>
            /// <returns>スケーリング後のベクトル</returns>
            public static Vector3D operator /(Vector3D vec, double scale)
            {
                return new Vector3D(vec.X / scale, vec.Y / scale, vec.Z / scale);
            }

            /// <summary>
            /// ベクトルの長さを取得します
            /// </summary>
            public double Length
            {
                get
                {
                    return Math.Sqrt(this.X * this.X + this.Y * this.Y + this.Z * this.Z);
                }
            }

            /// <summary>
            /// 配列形式でもアクセス可能とするインデクサ
            /// </summary>
            /// <param name="i"></param>
            /// <returns></returns>
            public double this[int i]
            {
                set
                {
                    switch (i)
                    {
                        case 0: X = value; break;
                        case 1: Y = value; break;
                        case 2: Z = value; break;
                    }
                }

                get
                {
                    switch (i)
                    {
                        case 0: return X;
                        case 1: return Y;
                        case 2: return Z;
                    }
                    return 0;
                }
            }

            /// <summary>
            /// 値を配列として取得します
            /// </summary>
            /// <returns></returns>
            public double[] ToArray()
            {
                return new double[] { X, Y, Z };
            }

            /// <summary>
            /// 値をコンマ区切りの文字列で取得します
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return String.Format("{0:F3}, {1:F3}, {2:F3}", X, Y, Z);
            }

            /// <summary>
            /// 全成分が0のベクトル
            /// </summary>
            public static Vector3D Zero = new Vector3D(0, 0, 0);
        }

        /// <summary>
        /// 4×4行列
        /// </summary>
        public struct Matrix
        {
            double[] Value;

            /// <summary>
            /// 配列を元に行列を作成します
            /// </summary>
            /// <param name="value"></param>
            public Matrix(double[] value)
            {
                Value = new double[16];
                value.CopyTo(Value, 0);
            }

            /// <summary>
            /// 値を配列として取得します
            /// </summary>
            /// <returns></returns>
            public double[] ToArray()
            {
                return Value;
            }

            /// <summary>
            /// 配列形式でもアクセス可能とするインデクサ
            /// </summary>
            /// <param name="i"></param>
            /// <returns></returns>
            public double this[int i]
            {
                get { return Value[i]; }
                set { Value[i] = value; }
            }

            /// <summary>
            /// 値をコンマ区切りの文字列で取得します
            /// </summary>
            /// <returns></returns>
            public override string ToString()
            {
                return
                    String.Format("[ [{0:F3}, {1:F3}, {2:F3}, {3:F3}]^T, [{4:F3}, {5:F3}, {6:F3}, {7:F3}]^T, [{8:F3}, {9:F3}, {10:F3}, {11:F3}]^T, [{12:F3}, {13:F3}, {14:F3}, {15:F3}]^T ]", Value);
            }
        }
        #endregion

    }
}
