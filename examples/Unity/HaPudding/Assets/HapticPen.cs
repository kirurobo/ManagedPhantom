using UnityEngine;
using System.Collections;
using ManagedPhantom;

/// <summary>
/// PHANTOMのサンプル
/// </summary>
/// <description>
/// このクラスを手先に相当するオブジェクトにアタッチして下さい
/// </description>
public class HapticPen : MonoBehaviour {

	private SimplePhantomUnity Phantom = null;
	private Vector3 HandPosition;
	private Quaternion HandRotation;

	/// <summary>
	/// PHANTOM向けに力を計算するための目標座標 [mm]
	/// </summary>
	private Vector3 TargetPosition = Vector3.zero;

	/// <summary>
	/// PHANTOM向けに力を計算するためのペン先座標 [mm]
	/// </summary>
	private Vector3 TipPosition = Vector3.zero;

	/// <summary>
	/// ペン先のオブジェクト
	/// </summary>
	public Transform HandTipTransform;
	
	/// <summary>
	/// このオブジェクトから引き離すように力を発生
	/// </summary>
	public Transform TargetTransform;

	/// <summary>
	/// 発生する斥力
	/// </summary>
	public float ForceEfficent = 0.5f;
	
	/// <summary>
	/// ターゲットからこの距離離れたときに、指定斥力となる [mm]
	/// </summary>
	public float Radius = 50.0f;

	/// <summary>
	/// 安全のため発揮力上限 [N]
	/// </summary>
	public float MaxForce = 0.8f;

	/// <summary>
	/// 最初に有効化されたとき1回のみ実行
	/// </summary>
	void OnEnable() {
		HandPosition = Vector3.zero;
		HandRotation = Quaternion.identity;

		// PHANTOMの初期化
		if (Phantom == null) {
			Phantom = new SimplePhantomUnity();

			// 繰り返し実行させるメソッドを指定
			Phantom.AddSchedule(PhantomUpdate);

			// 繰り返し処理を開始
			Phantom.Start();
		}
	}

	/// <summary>
	/// 無効化されたとき（終了時）に実行
	/// </summary>
	void OnDisable() {
		// PHANTOMの利用を終了
		if (Phantom != null) {
			Phantom.Close ();
			Phantom = null;
		}
	}

	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		// 単位を今回のシーン用[cm]に変換してオブジェクトに適用
		this.transform.position = HandPosition * 0.1f;
		this.transform.rotation = HandRotation;

		// 力の中心座標を更新
		if (TargetTransform != null) {
			TargetPosition = TargetTransform.position * 10.0f;	// 単位は[mm]に直す
		}
		// ペン先座標を更新
		if (HandTipTransform != null) {
			TipPosition = HandTipTransform.position * 10.0f;	// 単位は[mm]に直す
		} else {
			TipPosition = HandPosition;
		}
	}

	/// <summary>
	/// PPHANTOM の周期（デフォルト1kHz）で繰り返し呼ばれるメソッド
	/// </summary>
	/// <description>この中ではPHANTOMの座標系/単位</description>
	/// <returns><c>true</c>, if update was phantomed, <c>false</c> otherwise.</returns>
	bool PhantomUpdate () {
		// 手先（ジンバル部分）の座標を取得 [mm]
		HandPosition = Phantom.GetPosition();

		// 手先姿勢を取得 [mm]
		HandRotation = Phantom.GetRotation();

		// PHANTOMの発揮力 [N]
		Vector3 force;

		// ターゲットから引き離す方向に力をかける
		//   今回、内容は適当。
		//   本当は Unity の物理演算結果から力を持ってきたい
		//   でもやり方分らない (>_<)
		Vector3 vec = TipPosition - TargetPosition;
		float mag = vec.magnitude;
		// 0.05
		if (mag < 0.5f) {
			// 距離が 0.5 [mm] より目標に近いければ発揮力無しとする
			// 大きくなりすぎるのと、向きが不安定になるため
			force = Vector3.zero;
		} else {
			// 発揮力は目標に近づくと指数的に上昇
			float forceMag = ForceEfficent * Mathf.Log(Radius / mag) + ForceEfficent;
			if (forceMag < 0.0f) forceMag = 0.0f;

			// 安全のため最大発揮力を超える力はカット
			if (forceMag > MaxForce) {
				forceMag = MaxForce;
			}

			// ベクトルを正規化して、求めた大きさを掛ける
			force = vec * forceMag / mag;
		}

		// PHANTOMに対し力を指定
		Phantom.SetForce(force);

		return true;
	}
}
