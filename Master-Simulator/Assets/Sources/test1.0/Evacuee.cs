using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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
    [Header("Evacuee Situations")]
    public bool isEvacuate = false;
    [Header("Evacuee Targets")]
    public bool isFollowingDrone = false;
    public GameObject FollowTarget;
    public float TargetDistance;

    private GameObject followedDrone = null;
    private List<string> excludeTowers;

    private EnvManager _env;
    private string LogPrefix = "Evacuee: ";
    private NavMeshAgent navMeshAgent = null;
    private LineRenderer lineRenderer;
    

    void Start() {
        //デフォルトでは自身の1つ上の親オブジェクトをフィールドとして設定
        Field = transform.parent.gameObject;
        _env = Field.GetComponent<EnvManager>();
        excludeTowers = new List<string>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.speed = Speed;
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.positionCount = 0;

        transform.position = new Vector3(transform.position.x, 1.5f, transform.position.z);
    }
    
    void Update() {
        SearchDrone();
        if(!isFollowingDrone || FollowTarget == null) {
            List<GameObject> towers = SearchTowers(excludeTowers);
            if(towers.Count > 0) {
                FollowTarget = towers[0]; //最短距離のタワーを目標に設定
            }
        }
        if(FollowTarget != null) {
            Move();
        }
    }

    void FixedUpdate() {
        if(FollowTarget != null) {
            TargetDistance = Vector3.Distance(transform.localPosition, FollowTarget.transform.localPosition);
        }
        DrawPath();
    }

    void OnTriggerEnter(Collider other) {
        Debug.Log(LogPrefix + "OnTriggerEnter: " + other.tag);
    }

    /// <summary>
    /// タワーへの避難を行う
    /// </summary>
    /// <param name="tower">タワーオブジェクト</param>
    public void Evacuation(GameObject targetTower) {
        //Towerクラスを取得
        Tower tower = targetTower.GetComponent<Tower>();
        if(tower.currentCapacity > 0) {
            tower.NowAccCount++;
            isEvacuate = true;
            //避難処理が完了した場合、自身を非アクティブ化
            if(isFollowingDrone && followedDrone != null) {
                SendRemoveSignalForDrone(followedDrone);
            }
            gameObject.SetActive(false);
        } else { //キャパシティがいっぱいの場合、次のタワーを探す
            excludeTowers.Add(tower.uuid);
            List<GameObject> towers = SearchTowers(excludeTowers);
            if(towers.Count > 0) {
                FollowTarget = towers[0]; //最短距離のタワーを目標に設定
            }
        }
    }


    /// <summary>
    /// 目的地に向かって移動する
    /// </summary>
    private void Move() {
        if(navMeshAgent == null) {
            return;
        }
        Vector3 destination = new Vector3(FollowTarget.transform.position.x, transform.position.y, FollowTarget.transform.position.z);
        navMeshAgent.SetDestination(destination);
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
    /// <param name="excludeTowerUUIDs">除外するタワーのUUID.未指定の場合はnull</param>
    /// <returns>localField内のTowerオブジェクトのリスト</returns>
    private List<GameObject> SearchTowers(List<string> excludeTowerUUIDs = null) {
        List<GameObject> towers = _env.Util.GetGameObjectsFromTagOnLocal(Field, Tags.Tower);
        List<GameObject> sortedTowers = new List<GameObject>();
        foreach (var tower in towers) {
            if(excludeTowerUUIDs != null && excludeTowerUUIDs.Contains(tower.GetComponent<Tower>().uuid)) {
                continue;
            }
            sortedTowers.Add(tower);
        }

        sortedTowers.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
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

    private void DrawPath() {
        if(navMeshAgent == null || isFollowingDrone) {
            return;
        }
        if(navMeshAgent.path.corners.Length < 2) {
            return;
        }
        lineRenderer.positionCount = navMeshAgent.path.corners.Length;
        lineRenderer.SetPosition(0, transform.position);
        for (int i = 1; i < navMeshAgent.path.corners.Length; i++) {
            lineRenderer.SetPosition(i, navMeshAgent.path.corners[i]);
        }
    }
}
