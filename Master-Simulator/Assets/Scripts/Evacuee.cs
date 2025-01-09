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
    public enum EvacueeModelModes {
        Guided, // 誘導タスク
        Search // 探索タスク
    }
    public EvacueeModelModes ModelMode = EvacueeModelModes.Guided;
    public GameObject Field;
    [Header("Evacuee Parameters")]
    public int Age; // 年齢
    public string Gender; // 性別
    public float Speed; //移動速度
    public float SearchRadius = 100.0f; //探索範囲
    [Header("Evacuee Situations")]
    public bool isEvacuate = false;
    [Header("Evacuee Targets")]
    public bool isFollowingDrone = false;
    public GameObject FollowTarget;
    public float TargetDistance;
    public bool IsPathFind = false;

    private GameObject followedDrone = null;
    private List<string> excludeTowers;

    private EnvManager _env;
    private string LogPrefix = "Evacuee: ";
    public NavMeshAgent navMeshAgent = null;
    [Header("探索タスクにおける視界設定")]
    public float viewRadius = 10f; // 視界の半径
    public float viewAngle = 60f; // 視界の角度（度単位）
    public LayerMask detectionLayer; // 検出対象のレイヤーマスク    private LineRenderer lineRenderer;
    private LineRenderer lineRenderer;


    void Awake() {
        navMeshAgent = GetComponent<NavMeshAgent>();
        navMeshAgent.speed = Speed;
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.positionCount = 0;
        // タイミングによりnullになる場合があるため、ここで初期化
        Field = transform.parent.gameObject;
        _env = Field.GetComponent<EnvManager>();
        excludeTowers = new List<string>();

        //transform.position = new Vector3(transform.position.x, 1.5f, transform.position.z);
    }

    void Start() {
        //デフォルトでは自身の1つ上の親オブジェクトをフィールドとして設定
        Field = transform.parent.gameObject;
        _env = Field.GetComponent<EnvManager>();
        excludeTowers = new List<string>();

        if(_env.SimulateMode == EnvManager.SimulateModeSetting.EvacueesOnly) {
            List<GameObject> towers = SearchTowers(excludeTowers);
            if(towers.Count > 0) {
                FollowTarget = towers[0]; //最短距離のタワーを目標に設定
            }
        }
    }
    
    void Update() {
        
        if(_env.SimulateMode == EnvManager.SimulateModeSetting.SearchAgentModel && !isFollowingDrone && FollowTarget == null) {
            DetectDroneInView();
        }

        Move();
        
        IsPathFind = navMeshAgent.pathPending ? false : true;
        navMeshAgent.speed = Speed;
    }

    void FixedUpdate() {
        /*
        if(FollowTarget != null) {
            TargetDistance = Vector3.Distance(transform.position, FollowTarget.transform.position);
        }
        DrawPath();
        */
    }

    // 視界範囲をGizmosで視覚化（エディタ用）
    void OnDrawGizmosSelected() {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, viewRadius);

        // 視界角度を描画
        Vector3 forward = transform.forward;
        Quaternion leftRayRotation = Quaternion.Euler(0, -viewAngle / 2, 0);
        Quaternion rightRayRotation = Quaternion.Euler(0, viewAngle / 2, 0);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, leftRayRotation * forward * viewRadius);
        Gizmos.DrawRay(transform.position, rightRayRotation * forward * viewRadius);
    }

    void OnTriggerEnter(Collider other) {
        //Debug.Log(LogPrefix + "OnTriggerEnter: " + other.tag);
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
                //誘導されていたドローンエージェントのカウントを更新
                var agent = followedDrone.GetComponent<DroneNavAgent>();
                agent.guidedCount += 1;
                agent.AddReward(1.0f);
                SendRemoveSignalForDrone(followedDrone);
            }
            gameObject.SetActive(false);
        } else { //キャパシティがいっぱいの場合、次のタワー or ドローンを探す
            if(_env.SimulateMode == EnvManager.SimulateModeSetting.AgentsModel) {
                TrackingDrone();
            } else if(_env.SimulateMode == EnvManager.SimulateModeSetting.EvacueesOnly) {
                excludeTowers.Add(tower.uuid);
                List<GameObject> towers = SearchTowers(excludeTowers);
                if(towers.Count > 0) {
                    FollowTarget = towers[0]; //最短距離のタワーを目標に設定
                }
            }
        }
    }


    /// <summary>
    /// 目的地に向かって移動する
    /// </summary>
    private void Move() {
        if(FollowTarget == null) {
            return;
        }
        Vector3 destination = new Vector3(FollowTarget.transform.localPosition.x, transform.localPosition.y, FollowTarget.transform.localPosition.z);
        navMeshAgent.SetDestination(destination);
    }

    private void SearchDroneInRange() {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, SearchRadius);
        foreach (var hitCollider in hitColliders) {
            if (hitCollider.CompareTag(Tags.Agent)) {
                isFollowingDrone = true;
                FollowTarget = hitCollider.gameObject;
                if(followedDrone != null) { //前に追跡していたドローンがいた場合、リストから削除
                    SendRemoveSignalForDrone(followedDrone);
                }
                // 直前の追跡ドローンを更新
                followedDrone = hitCollider.gameObject;
                HidePath();
                SendAddSignalForDrone(followedDrone);
                return;
            }
        }
        // 探知圏外
        isFollowingDrone = false;
        FollowTarget = null;
        if(followedDrone != null) {
            SendRemoveSignalForDrone(followedDrone);
            followedDrone = null;
        }
    }

    public void TrackingDrone() {
        // 最短距離のドローンを探す
        GameObject[] agents = GameObject.FindGameObjectsWithTag(Tags.Agent);
        List<GameObject> sortedAgents = new List<GameObject>();
        foreach (var agent in agents) {
            sortedAgents.Add(agent);
        }
        sortedAgents.Sort((a, b) => Vector3.Distance(a.transform.position, transform.position).CompareTo(Vector3.Distance(b.transform.position, transform.position)));
        
        FollowTarget = sortedAgents[0];
        isFollowingDrone = true;
        if(followedDrone != null) { //前に追跡していたドローンがいた場合、リストから削除
            SendRemoveSignalForDrone(followedDrone);
        }
        // 直前の追跡ドローンを更新
        followedDrone = sortedAgents[0];
        HidePath();
        SendAddSignalForDrone(followedDrone);
    }
    
    /// <summary>
    /// タグ名からタワーを検索する。こちらは探索範囲関係なく、フィールドに存在する全てのタワーを検索し、
    /// 距離別にソートして返す
    /// </summary>
    /// <param name="excludeTowerUUIDs">除外するタワーのUUID.未指定の場合はnull</param>
    /// <returns>localField内のTowerオブジェクトのリスト</returns>
    private List<GameObject> SearchTowers(List<string> excludeTowerUUIDs = null) {
        GameObject[] towers = GameObject.FindGameObjectsWithTag(Tags.Tower);
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
        DroneNavAgent agent = drone.GetComponent<DroneNavAgent>();
        // 既に誘導中の場合は無視(リストに含まれている場合は無視)
        if(agent.currentGuidedEvacuees.Contains(gameObject)) {
            return;
        }
        agent.currentGuidedEvacuees.Add(gameObject);
        agent.onAddEvacuee?.Invoke();
    }

    private void SendRemoveSignalForDrone(GameObject drone) {
        DroneNavAgent agent = drone.GetComponent<DroneNavAgent>();
        if(agent.currentGuidedEvacuees.Contains(gameObject)) {
            agent.currentGuidedEvacuees.Remove(gameObject);
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

    private void HidePath() {
        lineRenderer.positionCount = 0;
    }

    void DetectDroneInView() {
        // 視界内に入ったオブジェクトを取得
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, viewRadius, detectionLayer);

        foreach (var hitCollider in hitColliders) {
            // 特定のタグを持つオブジェクトだけを処理
            if (hitCollider.CompareTag(Tags.Agent)) {
                Vector3 directionToTarget = (hitCollider.transform.position - transform.position).normalized;
                float angleBetween = Vector3.Angle(transform.forward, directionToTarget);

                // 視野角内にある場合のみ処理
                if (angleBetween < viewAngle / 2f) {
                    // Raycastで遮蔽物がないか確認（オプション）
                    if (!Physics.Linecast(transform.position, hitCollider.transform.position, ~detectionLayer)) {
                        Debug.Log("視界内にタグ " + Tags.Agent + " のオブジェクトを検出: " + hitCollider.gameObject.name);
                        // ターゲットに設定
                        isFollowingDrone = true;
                        FollowTarget = hitCollider.gameObject;
                    }
                }
            }
        }
    }
}
