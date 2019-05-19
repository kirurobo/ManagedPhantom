using UnityEngine;
using System;
using ManagedPhantom;

/// <summary>
/// PHANTOMのサンプル
/// </summary>
/// <description>
/// このクラスを手先に相当するオブジェクトにアタッチして下さい
/// </description>
public class HapticPen : MonoBehaviour {
	/// <summary>
	/// PHANTOM簡易操作クラスのインスタンス
	/// </summary>
	private SimplePhantomUnity Phantom;

	/// <summary>
	/// ジンバル部座標 [mm]
	/// </summary>
	private Vector3 HandPosition = Vector3.zero;
	
	/// <summary>
	/// ジンバル部速度 [mm/s]
	/// </summary>
	private Vector3 HandVelocity = Vector3.zero;
	
	/// <summary>
	/// 手先の姿勢
	/// </summary>
	private Quaternion HandRotation;
	
	/// <summary>
	/// PHANTOM向けに力を計算するためのペン先座標 [mm]
	/// </summary>
	public Vector3 TipPosition = Vector3.zero;

	/// <summary>
	/// 発揮力上限 [N]
	/// </summary>
	private float MaxForce = 3.0f;


	/// <summary>
	/// 提示する球体のリスト
	/// </summary>
	private PhantomRigidSphere[] SphereList;
	
	/// <summary>
	/// 1mmがUnityの長さいくつにあたるか [1/mm]
	/// </summary>
	public float UnitLength = 0.001f;


	/// <summary>
	/// 最初に有効化されたとき1回のみ実行
	/// </summary>
	void Awake() {
        //Phantom = new SimplePhantomUnity();
        Phantom = SimplePhantomUnity.GetInstance();

		// 繰り返し実行させるメソッドを指定
		Phantom.AddSchedule(PhantomUpdate);
	}

	/// <summary>
	/// 有効化されたとき
	/// </summary>
	void OnEnable() {
		Phantom.Start();
	}

	/// <summary>
	/// 無効化されたとき
	/// </summary>
	void OnDisable() {
		Phantom.Stop();
	}

    /// <summary>
    /// 現状、終了しようとしても応答しなくなるため、スタンドアローンならば強制的に終了
    /// </summary>
    void OnApplicationQuit()
    {
        // See https://forum.unity.com/threads/problem-with-callbacks.87513/
#if UNITY_EDITOR
        Phantom.Close();

        Debug.Log("EDITOR NO CLOSE...");
#elif UNITY_STANDALONE_WIN //Seemingly fires in editor as well...
        Phantom.Close();
        System.Diagnostics.Process.GetCurrentProcess().Kill();
#endif
    }

    /// <summary>
    /// 開始時の処理
    /// </summary>
    void Start () {
		// 球体の一覧を作成
		SphereList = GameObject.FindObjectsOfType<PhantomRigidSphere>();
	}
	
	/// <summary>
	/// 毎フレームの処理
	/// </summary>
	void Update () {
		// PHANTOM のボタン押下状況を更新して取得（GetButtonDown, Up のために必要）
		Phantom.UpdateButtons();

		// 単位を今回のシーン用に変換してオブジェクトに適用
		this.transform.localPosition = HandPosition * UnitLength;
		this.transform.localRotation = HandRotation;

		// ペン先側のボタンが押された
		if (Phantom.GetButtonDown(Buttons.Button1)) {
			SphereList[0].gameObject.SetActive(false);
		}

		if (Phantom.GetButtonUp(Buttons.Button1)) {
			SphereList[0].gameObject.SetActive(true);
		}

		// ペン手元側のボタンが押された
		if (Phantom.GetButtonDown(Buttons.Button2)) {
		}
	}

	/// <summary>
	/// PHANTOM の周期（デフォルト1kHz）で繰り返し呼ばれるメソッド
	/// </summary>
	/// <description>この中ではPHANTOMの座標系/単位</description>
	/// <returns><c>true</c>, if update was phantomed, <c>false</c> otherwise.</returns>
	bool PhantomUpdate () {
		// 手先（ジンバル部分）の座標を取得 [mm]
		HandPosition = Phantom.GetPosition();
		
		// 手先の速度を取得 [mm/s]
		HandVelocity = Phantom.GetVelocity();

		// 手先姿勢を取得
		HandRotation = Phantom.GetRotation();

		// ペン先端の座標を取得 [mm]
		TipPosition = Phantom.GetTipPosition();


		// PHANTOMの発揮力 [N]
		Vector3 force = Vector3.zero;

		// 球体から受ける力を計算
		if (SphereList != null) {
			foreach (PhantomRigidSphere sphere in SphereList) {
				force += sphere.CalculateForce(TipPosition, HandVelocity);
			}
		}

		// 力の上限を超えないようにする
		if (force.sqrMagnitude > (MaxForce * MaxForce)) {
			force.Normalize();
			force *= MaxForce;
		}
		
		// PHANTOMに対し力を指定
		Phantom.SetForce(force);
		
		return true;
	}
}
