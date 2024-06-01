using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityFuncs;
using Constants;

/// <summary>
/// 避難者に関するスクリプト
/// </summary>
public class Evacuee : MonoBehaviour {
    public GameObject Field;
    [Header("Evacuee Parameters")]
    public int Age; // 年齢
    public string Gender; // 性別
    public float Speed; //移動速度
    public int SearchRadius; //探索範囲
    public float EvacuationJudgmentDistance; //避難判定距離
    [Header("Evacuee Situations")]
    public bool isEvacuate = false;
    [Header("Evacuee Targets")]
    public bool isFollowingDrone = false;
    public GameObject FollowTarget;
    public float TargetDistance;

    private GameObject followedDrone = null;

    private EnvManager _env;

    void Start() {
        //デフォルトでは自身の1つ上の親オブジェクトをフィールドとして設定
        Field = transform.parent.gameObject;
        _env = Field.GetComponent<EnvManager>();
    }
    
    void Update() {
        SearchDrone();
        if(!isFollowingDrone || FollowTarget == null) {
            List<GameObject> towers = SearchTowers();
            if(towers.Count > 0) {
                FollowTarget = towers[0]; //最短距離のタワーを目標に設定
            }
        }
        if(FollowTarget != null) {
            Move();
        }
        //避難タワーに到達した場合、避難処理を行う
        if(!isFollowingDrone && Vector3.Distance(transform.localPosition, FollowTarget.transform.localPosition) < EvacuationJudgmentDistance) {
            Evacuation(FollowTarget);
        }
    }

    void FixedUpdate() {
        if(FollowTarget != null) {
            TargetDistance = Vector3.Distance(transform.localPosition, FollowTarget.transform.localPosition);
        }
    }

    /// <summary>
    /// タワーへの避難を行う
    /// </summary>
    /// <param name="tower">タワーオブジェクト</param>
    private void Evacuation(GameObject targetTower) {
        //Towerクラスを取得
        Tower tower = targetTower.GetComponent<Tower>();
        if(tower.currentCapacity > 0) {
            tower.NowAccCount++;
            isEvacuate = true;
            //避難処理が完了した場合、自身を非アクティブ化
            gameObject.SetActive(false);
        } else { //キャパシティがいっぱいの場合、次のタワーを探す
            List<GameObject> towers = SearchTowers(tower.uuid);
            if(towers.Count > 0) {
                FollowTarget = towers[0]; //最短距離のタワーを目標に設定
            }
        }
    }


    /// <summary>
    /// 目的地に向かって移動する
    /// </summary>
    private void Move() {
        Vector3 pos = new Vector3(FollowTarget.transform.position.x, 1.3f, FollowTarget.transform.position.z);
        transform.position = Vector3.MoveTowards(transform.position, pos, Speed * Time.deltaTime);
    }


    private void SearchDrone() {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, SearchRadius);
        foreach (var hitCollider in hitColliders) {
            if (hitCollider.CompareTag(Tags.Agent)) {
                isFollowingDrone = true;
                FollowTarget = hitCollider.gameObject;
                followedDrone = hitCollider.gameObject;
                SendAddSignalForDrone(followedDrone);
                return;
            }
        }
        //Debug用に探索範囲を表示
        Debug.DrawLine(transform.position, transform.position + new Vector3(SearchRadius, 0, 0), Color.green);
        isFollowingDrone = false;
        FollowTarget = null;
        if(followedDrone != null) {
            SendRemoveSignalForDrone(followedDrone);
            followedDrone = null;
        }
    }
    
    /// <summary>
    /// タグ名からタワーを検索する。こちらは探索範囲関係なく、フィールドに存在する全てのタワーを検索し、
    /// 距離別にソートして返す
    /// </summary>
    /// <param name="excludeUUID">除外するタワーのUUID.未指定の場合はnull</param>
    /// <returns>localField内のTowerオブジェクトのリスト</returns>
    private List<GameObject> SearchTowers(string excludeUUID=null) {
        List<GameObject> towers = _env.Util.GetGameObjectsFromTagOnLocal(Field, Tags.Tower);
        Debug.Log($"Towers Count: {towers.Count}");
        List<GameObject> sortedTowers = new List<GameObject>();
        foreach (var tower in towers) {
            if(excludeUUID != null && tower.GetComponent<Tower>().uuid == excludeUUID) {
                continue;
            }
            sortedTowers.Add(tower);
        }
        sortedTowers.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
        //Debug用にマーカーを表示
        foreach (var tower in sortedTowers) {
            Debug.DrawLine(transform.position, tower.transform.position, Color.red);
        }
        return sortedTowers;
    }


    private void SendAddSignalForDrone(GameObject drone) {
        DroneAgent agent = drone.GetComponent<DroneAgent>();
        // 既に誘導中の場合は無視(リストに含まれている場合は無視)
        if(agent.guidedEvacuees.Contains(gameObject)) {
            return;
        }
        agent.guidedEvacuees.Add(gameObject);
    }


    private void SendRemoveSignalForDrone(GameObject drone) {
        DroneAgent agent = drone.GetComponent<DroneAgent>();
        if(agent.guidedEvacuees.Contains(gameObject)) {
            agent.guidedEvacuees.Remove(gameObject);
        }
    }
}
