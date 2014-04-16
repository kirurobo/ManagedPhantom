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
	List<uint> ScheduleHandles;                     // HDAPIDでスケジューリングした際のハンドルを保持
	
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
	public bool IsRunning { get; private set; }
	
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
		ErrorCheck();

		result = callback();

		Hd.hdEndFrame(hHD);
		ErrorCheck();
		
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
		double qw = Math.Sqrt(1f + matrix[0] + matrix[5] + matrix[10]) / 2;
		double w = 4 * qw;
		double qx = (matrix[6] - matrix[9]) / w;
		double qy = (matrix[8] - matrix[2]) / w;
		double qz = (matrix[1] - matrix[4]) / w;
		return new Quaternion((float)-qx, (float)-qy, (float)qz, (float)qw);
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
		ErrorCheck();
		WorkspaceMinimum = new Vector3((float)val[0], (float)val[1], -(float)val[2]);
		WorkspaceMaximum = new Vector3((float)val[3], (float)val[4], -(float)val[5]);
		
		// 推奨可動範囲を取得
		Hd.hdGetDoublev(Hd.ParameterName.HD_USABLE_WORKSPACE_DIMENSIONS, val);
		ErrorCheck();
		UsableWorkspaceMinimum = new Vector3((float)val[0], (float)val[1], -(float)val[2]);
		UsableWorkspaceMaximum = new Vector3((float)val[3], (float)val[4], -(float)val[5]);
		
		// 机の高さを取得
		float[] offset = new float[1];
		Hd.hdGetFloatv(Hd.ParameterName.HD_TABLETOP_OFFSET, offset);
		ErrorCheck();
		TableTopOffset = (float)offset[0];
	}
	
	/// <summary>
	/// 直前のHDAPI呼び出しでエラーがあれば、例外を発生させます
	/// </summary>
	private void ErrorCheck()
	{
		Hd.ErrorInfo error;
		
		if (Hd.IsError(error = Hd.hdGetError()))
		{
			string message = Hd.GetErrorString(error.ErrorCode);
			
			Debug.LogError("HDAPI error : " + message);
			//throw new  UnityException("SimplePhantom : " + message);
		}
	}
	#endregion
}
