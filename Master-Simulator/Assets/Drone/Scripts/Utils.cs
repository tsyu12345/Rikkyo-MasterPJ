using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UtilityFuncs {
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

    /// <summary>
    /// ローカル内における、ゲームオブジェクトのタグ検索
    /// </summary>
    /// <param name="parent">検索する親オブジェクト</param>
    /// <param name="tag">検索対象の子オブジェクトのタグ名</param>
    /// <returns>
    /// ゲームオブジェクトのリスト
    /// </returns>
    /// 
    public List<GameObject> GetGameObjectsFromTagOnLocal(GameObject parent, string tag) {
        Transform[] childrens = parent.GetComponentsInChildren<Transform>();
        List<GameObject> result = new List<GameObject>();
        foreach (Transform child in childrens) {
            if (child.CompareTag(tag)) {
                result.Add(child.gameObject);
            }
        }
        return result;
    }
}
}
