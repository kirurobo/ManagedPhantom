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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManagedPhantom.Structs;

namespace ManagedPhantom.RigidPrimitives
{
    public class Orb
    {
        /// <summary>
        /// 球の中心座標 [mm]
        /// </summary>
        public Vector3D Position = Vector3D.Zero;

        /// <summary>
        /// 球の半径 [mm]
        /// </summary>
        public double Radius = 0;

        /// <summary>
        /// 球表面から押し込まれた際にかける力のバネ定数 [N/mm]
        /// </summary>
        public double Stiffness = 1.0;

        /// <summary>
        /// 球内部にいる際のダンピング係数 [Ns/mm]
        /// </summary>
        public double Dumping = 0.0;

        /// <summary>
        /// 発生力の最大値 [N]
        /// </summary>
        public double ForceLimit = 3.0;

        /// <summary>
        /// 位置と半径を指定して球を作成
        /// </summary>
        /// <param name="position">中心座標[mm]</param>
        /// <param name="radius">半径[mm]</param>
        public Orb(Vector3D position, double radius)
        {
            this.Position = position;
            this.Radius = radius;
        }

        /// <summary>
        /// 操作点が球に接触（進入）していた際に発生する力を求める
        /// </summary>
        /// <param name="tipPosition">操作点の座標 [mm]</param>
        /// <param name="tipVelocity">操作点の速度 [mm/s]</param>
        /// <returns></returns>
        public Vector3D CalculateForce(Vector3D tipPosition, Vector3D tipVelocity)
        {

            Vector3D vec = tipPosition - this.Position;
            double distance = vec.Length;

            // 球の外や真ん中では力なし
            if (distance >= this.Radius || distance == 0)
            {
                return Vector3D.Zero;
            }

            vec /= distance;	// 正規化
            double f = this.Stiffness * (this.Radius - distance);
            if (f > this.ForceLimit) f = this.ForceLimit;

            return (f * vec) - this.Dumping * tipVelocity;
        }

        /// <summary>
        /// 操作点が球に接触（進入）していた際に発生する力を求める（ダンピングなし）
        /// </summary>
        /// <param name="tipPosition">操作点の座標 [mm]</param>
        /// <returns></returns>
        public Vector3D CalculateForce(Vector3D tipPosition)
        {

            Vector3D vec = tipPosition - this.Position;
            double distance = vec.Length;

            // 球の外や真ん中では力なし
            if (distance >= this.Radius || distance == 0)
            {
                return Vector3D.Zero;
            }

            vec /= distance;	// 正規化
            double f = this.Stiffness * (this.Radius - distance);
            if (f > this.ForceLimit) f = this.ForceLimit;

            return (f * vec);
        }

    }
}
