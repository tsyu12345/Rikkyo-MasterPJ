using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 避難者に関するスクリプト
/// </summary>
public class Evacuee : MonoBehaviour {
    [Header("Evacuee Parameters")]
    public int Age; // 年齢
    public string Gender; // 性別
    public float Speed; //移動速度
    public int SearchRadius; //探索範囲
    [Header("Evacuee Targets")]
    public bool isFollowingDrone = false;
    public GameObject FollowTarget;
    private string DroneTag = "Agent";
    private string TowerTag = "Tower";

    void Update() {
        SearchDrone();
        Move();
    }


    /// <summary>
    /// 目的地に向かって移動する
    /// </summary>
    private void Move() {
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, FollowTarget.transform.localPosition, Speed * Time.deltaTime);
    }

    private void SearchDrone() {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, SearchRadius);
        foreach (var hitCollider in hitColliders) {
            if (hitCollider.CompareTag(DroneTag)) {
                isFollowingDrone = true;
                FollowTarget = hitCollider.gameObject;
                return;
            }
        }
    }
    
    /// <summary>
    /// タグ名からタワーを検索する。こちらは探索範囲関係なく、フィールドに存在する全てのタワーを検索し、
    /// 距離別にソートして返す
    /// TODO:今はグローバルに検索しかできていないので、フィールド内でのみ検索できるようにする
    /// </summary>
    /// <returns>localField内のTowerオブジェクトのリスト</returns>
    private List<GameObject> SearchTowers() {
        GameObject[] towers = GameObject.FindGameObjectsWithTag(TowerTag);
        List<GameObject> sortedTowers = new List<GameObject>();
        foreach (var tower in towers) {
            sortedTowers.Add(tower);
        }
        sortedTowers.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
        return sortedTowers;
    }
}
