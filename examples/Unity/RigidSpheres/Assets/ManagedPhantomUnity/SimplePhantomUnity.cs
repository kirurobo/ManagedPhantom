/**
 * ------------------------------------------------
 * ManagedPhantom
 * 
 * Simple PHANToM for Unity
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

using UnityEngine;
using System;
using System.Collections.Generic;
using ManagedPhantom;

/// <summary>
/// Unityで手軽にPHANTOMを利用するためのクラス
/// </summary>
/// <description>
/// 単位はPHANTOMに従い [mm] [mm/s] [N] 等です。
/// ただしUnityに合わせZ軸を反転させています。
/// </description>
public class SimplePhantomUnity {
	uint hHD = (uint)Hd.DeviceHandle.HD_INVALID_HANDLE;     // デバイスハンドル
	List<Hd.SchedulerCallback> CallbackMethods;     // 参照が無くなるとGCされるので、メソッドを保持
	private List<ulong> ScheduleHandles;                     // HDAPIDでスケジューリングした際のハンドルを保持
	private Buttons CurrentButtons = Buttons.None;	// 現在のPHANTOMボタン押下状況
	private Buttons LastButtons = Buttons.None;	// 前回Update時のPHANTOMボタン

	/// <summary>
	/// PHANToMに接続できていればtrue
	/// </summary>
	/// <value><c>true</c> if this instance is avairable; otherwise, <c>false</c>.</value>
	internal bool IsAvailable { get { return hHD != (uint)Hd.DeviceHandle.HD_INVALID_HANDLE; }}

	/// <summary>
	/// ジンバル部を基準としたペン先端座標 [mm] (PHANTOM座標系)
	/// </summary>
	public Vector3 TipOffset = new Vector3(0.0f, 0.0f, -40.0f);
	
	/// <summary>
	/// 可動範囲下限 [mm]
	/// </summary>
	public Vector3 WorkspaceMinimum { get; private set; }
	
	/// <summary>
	/// 可動範囲上限 [mm]
	/// </summary>
	public Vector3 WorkspaceMaximum { get; private set; }
	
	/// <summary>
	/// 推奨可動範囲下限 [mm]
	/// </summary>
	public Vector3 UsableWorkspaceMinimum { get; private set; }
	
	/// <summary>
	/// 推奨可動範囲上限 [mm]
	/// </summary>
	public Vector3 UsableWorkspaceMaximum { get; private set; }
	
	/// <summary>
	/// 机の面に相当するY座標 [mm]
	/// </summary>
	public float TableTopOffset { get; private set; }
	
	/// <summary>
	/// PHANTOMの処理が実行中なら true とする
	/// </summary>
	public bool IsRunning = false;


	/// <summary>
	/// 非同期で呼ばれるメソッドです
	/// </summary>
	/// <returns>true:要継続, false:終了</returns>
	public delegate bool Callback();
	
	
	/// <summary>
	/// デフォルトのデバイスに接続します
	/// </summary>
	public SimplePhantomUnity()
	{
		IsRunning = false;
		
		// デフォルトのデバイスを準備
		hHD = Hd.hdInitDevice(Hd.DeviceHandle.HD_DEFAULT_DEVICE);
		ErrorCheck("Initialize device");
		
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

			hHD = (uint)Hd.DeviceHandle.HD_INVALID_HANDLE;
		}
	}
	
	#region スケジューリング関連
	/// <summary>
	/// 非同期処理を開始します
	/// </summary>
	public void Start()
	{
		if (!IsAvailable || IsRunning) return;
		
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
		if (!IsAvailable || !IsRunning) return;
		
		IsRunning = false;

		////System.Threading.Thread.Sleep (10);
		//foreach (uint handle in ScheduleHandles)
		//{
		//	Hd.hdWaitForCompletion(handle, Hd.WaiteCode.HD_WAIT_INFINITE);
		//	ErrorCheck("Waiting for completion");
		//}

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
		ErrorCheck("BeginFrame");

		result = callback();

		Hd.hdEndFrame(hHD);
		ErrorCheck("EndFrame");
		
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
		ErrorCheck("Set scheduler rate");
	}
	
	#endregion
	
	#region 情報取得メソッド
	/// <summary>
	/// 現在のPHANTOM手先座標を返します
	/// </summary>
	/// <returns>ジンバル座標 [mm]</returns>
	public Vector3 GetPosition()
	{
		double[] position = new double[3] { 0, 0, 0 };
		Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_POSITION, position);
		return new Vector3((float)position[0], (float)position[1], -(float)position[2]);
	}
	
	/// <summary>
	/// 現在のPHANTOM手先速度を返します
	/// </summary>
	/// <returns>速度ベクトル [mm/s]</returns>
	public Vector3 GetVelocity()
	{
		double[] velocity = new double[3] { 0, 0, 0 };
		Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_VELOCITY, velocity);
		return new Vector3((float)velocity[0], (float)velocity[1], -(float)velocity[2]);
	}

	/// <summary>
	/// ペン先端の座標を返します
	/// </summary>
	/// <returns>ペン先端座標 [mm]</returns>
	public Vector3 GetTipPosition()
	{
		double[] position = new double[3] { 0, 0, 0 };
		double[] matrix = new double[16];
		Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_POSITION, position);
		Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_TRANSFORM, matrix);
		
		Vector3 tipPosition;
		tipPosition.x = (float)(position[0] + matrix[0] * TipOffset.x + matrix[4] * TipOffset.y + matrix[8] * TipOffset.z);
		tipPosition.y = (float)(position[1] + matrix[1] * TipOffset.x + matrix[5] * TipOffset.y + matrix[9] * TipOffset.z);
		tipPosition.z = -(float)(position[2] + matrix[2] * TipOffset.x + matrix[6] * TipOffset.y + matrix[10] * TipOffset.z);
		
		return tipPosition;
	}

	//　↓たぶん誰も使わないし、Unity座標系への変換をしていないのでコメントアウト
//	/// <summary>
//	/// 現在のPHANTOMジンバル姿勢を返します
//	/// <remarks>手先の姿勢ではありません。ジンバル部エンコーダの値です。</remarks>
//	/// </summary>
//	/// <returns>ジンバル部分の内、根元からペン部にかけて X～Z に対応した角度 [rad]</returns>
//	public Vector3 GetGimbalAngles()
//	{
//		double[] gimbals = new double[3] { 0, 0, 0 };
//		Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_GIMBAL_ANGLES, gimbals);
//		return new Vector3((float)gimbals[0], (float)gimbals[1], (float)gimbals[2]);
//	}
	
	/// <summary>
	/// 現在のPHANTOM手先姿勢を返します
	/// </summary>
	/// <returns>姿勢を表すクォータニオン</returns>
	public Quaternion GetRotation()
	{
		double[] matrix = new double[16];
		Hd.hdGetDoublev(Hd.ParameterName.HD_CURRENT_TRANSFORM, matrix);
//
//		double qw = Math.Sqrt(1f + matrix[0] + matrix[5] + matrix[10]) / 2;
//		double w = 4 * qw;
//		double qx = (matrix[6] - matrix[9]) / w;
//		double qy = (matrix[8] - matrix[2]) / w;
//		double qz = (matrix[1] - matrix[4]) / w;
//		return new Quaternion((float)-qx, (float)-qy, (float)qz, (float)qw);
		
		double t = 1.0 + matrix[0] + matrix[5] + matrix[10];
		double s;
		double qw, qx, qy, qz;
		if (t >= 1.0) {
			s = 0.5 / Math.Sqrt(t);
			qw = 0.25 / s;
			qx = (matrix[6] - matrix[9]) * s;
			qy = (matrix[8] - matrix[2]) * s;
			qz = (matrix[1] - matrix[4]) * s;
		} else {
			double max;
			if (matrix[5] > matrix[10]) {
				max = matrix[5];
			} else {
				max = matrix[10];
			}
			
			if (max < matrix[0]) {
				t = Math.Sqrt(matrix[0] - (matrix[5] + matrix[10]) + 1.0);
				s = 0.5 / t;
				qw = (matrix[6] - matrix[9]) * s;
				qx = t * 0.5;
				qy = (matrix[1] + matrix[4]) * s;
				qz = (matrix[8] + matrix[2]) * s;
			} else if (max == matrix[5]) {
				t = Math.Sqrt(matrix[5] - (matrix[10] + matrix[0]) + 1.0);
				s = 0.5 / t;
				qw = (matrix[8] - matrix[2]) * s;
				qx = (matrix[1] + matrix[4]) * s;
				qy = t * 0.5;
				qz = (matrix[6] + matrix[9]) * s;
			} else {
				t = Math.Sqrt(matrix[10] - (matrix[0] + matrix[5]) + 1.0);
				s = 0.5 / t;
				qw = (matrix[1] - matrix[4]) * s;
				qx = (matrix[8] + matrix[2]) * s;
				qy = (matrix[6] + matrix[9]) * s;
				qz = t * 0.5;
			}
		}
		return new Quaternion(-(float)qx, -(float)qy, (float)qz, (float)qw);
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
	public bool GetButtonDown(Buttons button) {
		return (((LastButtons & button) == Buttons.None) && ((CurrentButtons & button) == button));
	}
	
	/// <summary>
	/// 指定されたボタンがまさに離されたところならばtrueを返す
	/// </summary>
	/// <returns><c>true</c>, if button was up, <c>false</c> otherwise.</returns>
	/// <param name="button">Button.</param>
	public bool GetButtonUp(Buttons button) {
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
	public void SetForce(Vector3 force)
	{
		double[] forceArray = new double[3];
		forceArray[0] = force.x;
		forceArray[1] = force.y;
		forceArray[2] = -force.z;
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
		WorkspaceMinimum = new Vector3((float)val[0], (float)val[1], -(float)val[2]);
		WorkspaceMaximum = new Vector3((float)val[3], (float)val[4], -(float)val[5]);
		
		// 推奨可動範囲を取得
		Hd.hdGetDoublev(Hd.ParameterName.HD_USABLE_WORKSPACE_DIMENSIONS, val);
		ErrorCheck("Getting usable workspace");
		UsableWorkspaceMinimum = new Vector3((float)val[0], (float)val[1], -(float)val[2]);
		UsableWorkspaceMaximum = new Vector3((float)val[3], (float)val[4], -(float)val[5]);
		
		// 机の高さを取得
		float[] offset = new float[1];
		Hd.hdGetFloatv(Hd.ParameterName.HD_TABLETOP_OFFSET, offset);
		ErrorCheck("Getting table-top offset");
		TableTopOffset = (float)offset[0];
	}

	/// <summary>
	/// 直前のHDAPI呼び出しでエラーがあれば、例外を発生させます
	/// </summary>
	static private void ErrorCheck() {
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
			string errorMessage = Hd.GetErrorString(error.ErrorCode);

			if (situation.Equals("")) {
				throw new UnityException("HDAPI : " + errorMessage);
			} else {
				throw new UnityException(situation + " / HDAPI : " + errorMessage);
			}
		}
	}
	#endregion
}
