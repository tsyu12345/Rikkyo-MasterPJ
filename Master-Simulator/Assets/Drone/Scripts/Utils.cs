using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オブジェクトの操作には関係ないが、汎用的な処理をまとめたクラス
/// </summary>
public class Utils : MonoBehaviour {

    /// <summary>
    /// 文字列データをVector3に変換します
    /// </summary>
    /// <param name="vectorString">(x, y, z)のVector3文字列</param>
    /// <returns>Vector3 データ</returns>
    public static Vector3 ConvertStringToVector3(string vectorString) {
        vectorString = vectorString.TrimStart('(').TrimEnd(')');
        string[] sArray = vectorString.Split(',');

        Vector3 result = new Vector3(
            float.Parse(sArray[0]),
            float.Parse(sArray[1]),
            float.Parse(sArray[2])
        );

        return result;
    }
}
