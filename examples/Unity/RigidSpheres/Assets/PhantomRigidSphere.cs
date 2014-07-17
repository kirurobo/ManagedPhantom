/**
 * ------------------------------------------------
 * ManagedPhantom
 * 
 * Rigid sphere 
 * 
 * Copyright (c) 2014 Kirurobo
 * http://twitter.com/kirurobo
 * 
 * This software is released under the MIT License.
 * http://opensource.org/licenses/mit-license.php
 * 
 * ------------------------------------------------
 */

using UnityEngine;

public class PhantomRigidSphere : MonoBehaviour
{
	/// <summary>
	/// Unity座標系の長さ「1.0」がPHANToM座標系の何mmにあたるか  [mm]
	/// </summary>
	public float Scale = 1000f;

	/// <summary>
	/// 球の中心座標 [mm]
	/// </summary>
	public Vector3 Position = Vector3.zero;

	/// <summary>
	/// 球の半径 [mm]
	/// </summary>
	public float Radius = 0f;

	/// <summary>
	/// 球表面から押し込まれた際にかける力のバネ定数 [N/mm]
	/// </summary>
	public float Stiffness = 1.0f;

	/// <summary>
	/// 球内部にいる際のダンピング係数 [Ns/mm]
	/// </summary>
	public float Dumping = 0.0f;

	/// <summary>
	/// 発生力の最大値 [N]
	/// </summary>
	public float ForceLimit = 3.0f;

	private void InitializeShape() {
		this.Position = this.transform.position * Scale;
		this.Radius = this.transform.localScale.x * Scale * 0.5f;		// 球以外には非対応
		this.transform.hasChanged = false;
	}

	void Start() {
		InitializeShape();
	}

	void Update() {
		if (this.transform.hasChanged) {
			InitializeShape();
		}
	}

	/// <summary>
	/// 操作点が球に接触（進入）していた際に発生する力を求める
	/// </summary>
	/// <param name="tipPosition">操作点の座標 [mm]</param>
	/// <param name="tipVelocity">操作点の速度 [mm/s]</param>
	/// <returns></returns>
	public Vector3 CalculateForce(Vector3 tipPosition, Vector3 tipVelocity)
	{

	    Vector3 vec = tipPosition - this.Position;
	    float distance = vec.magnitude;

	    // 球の外や真ん中では力なし
	    if (distance >= this.Radius || distance == 0)
	    {
	        return Vector3.zero;
	    }

	    vec /= distance;	// 正規化
	    float f = this.Stiffness * (this.Radius - distance);
	    if (f > this.ForceLimit) f = this.ForceLimit;

	    return (f * vec) - this.Dumping * tipVelocity;
	}

	/// <summary>
	/// 操作点が球に接触（進入）していた際に発生する力を求める（ダンピングなし）
	/// </summary>
	/// <param name="tipPosition">操作点の座標 [mm]</param>
	/// <returns></returns>
	public Vector3 CalculateForce(Vector3 tipPosition)
	{

	    Vector3 vec = tipPosition - this.Position;
	    float distance = vec.magnitude;

	    // 球の外や真ん中では力なし
	    if (distance >= this.Radius || distance == 0)
	    {
	        return Vector3.zero;
	    }

	    vec /= distance;	// 正規化
	    float f = this.Stiffness * (this.Radius - distance);
	    if (f > this.ForceLimit) f = this.ForceLimit;

	    return (f * vec);
	}
}
