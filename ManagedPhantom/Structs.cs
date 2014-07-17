/**
 * ------------------------------------------------
 * ManagedPhantom
 * 
 * Basic structs
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

namespace ManagedPhantom.Structs
{

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
