using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;
using UtilityFuncs;
using Constants;

public class DroneNavAgent : Agent {
    
    [Header("Agent Parameters")]
    public float patrolRadius = 10f;    
    public List<GameObject> currentGuidedEvacuees = new List<GameObject>();
    public int guidedCount = 0;

    [Header("UI Elements")]
    private TextMeshPro currentGuidingCount;
    private TextMeshPro currentGoalCount;
    
    private EnvManager _env;
    private DroneController _controller;
    private Vector3 StartPos;

    private string LogPrefix = "DroneAgent: ";

    public delegate void OnAddEvacuee();
    public OnAddEvacuee onAddEvacuee;

    void Start() {
        _controller = GetComponent<DroneController>();
        _env = GetComponentInParent<EnvManager>();
        _env.Drones.Add(gameObject);
        _controller.RegisterTeam(gameObject.tag);
        _controller.onCrash += OnCrash;
        _env.OnEndEpisode += OnEndEpisodeHandler;
        // 初期位置を保存
        StartPos = transform.localPosition;

        currentGuidingCount = transform.Find("GuidingCounter").GetComponent<TextMeshPro>();
        currentGoalCount = transform.Find("GuidedCounter").GetComponent<TextMeshPro>();

        onAddEvacuee += () => {
            AddReward(0.1f);
        };
    }

    void Update() {
        currentGuidingCount.text = currentGuidedEvacuees.Count.ToString();
        currentGoalCount.text = guidedCount.ToString();
    }

    public override void Initialize() {
        Debug.Log(LogPrefix + "Initialize");
        //_env.init();
    }

    public override void OnEpisodeBegin() {
        Reset();
    }

    /// <summary>
    /// 観測情報
    /// １．自身の速度 Vector3
    /// ２．各避難タワーの位置 Vector3
    /// ３．各避難タワーの収容人数 int
    /// ４．現在誘導している避難者の数 int
    /// ５．全避難者の座標 Vector3    
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor) {
        //自身の位置・速度を観測情報に追加
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(_controller.NavAgent.speed);
        //現在誘導している避難者の数を観測情報に追加
        sensor.AddObservation(currentGuidedEvacuees.Count);

        //他のドローンの位置を観測情報に追加
        List<GameObject> otherAgents = GetOtherAgents();
        foreach(GameObject agent in otherAgents) {
            sensor.AddObservation(agent.transform.localPosition);
        }        
        //各避難タワーからの観測情報を追加
        //観測サイズを固定しないといけないので、最大数の避難タワーを観測情報に追加(残りは空：ZeroVector, -1)
        foreach (var towerObj in _env.Towers) {
            //sensor.AddObservation(tower.transform.localPosition);
            var tower = towerObj.GetComponent<Tower>();
            sensor.AddObservation(tower.currentCapacity);
        }
    
        //全避難者の座標を観測情報に追加
        List<GameObject> evacuees = _env.Util.GetGameObjectsFromTagOnLocal(_env.gameObject, Tags.Evacuee);
        for(int i = 0; i < _env.MaxEvacueeCount; i++) {
            if(i < evacuees.Count) {
                sensor.AddObservation(evacuees[i].transform.localPosition);
            } else {
                sensor.AddObservation(Vector3.zero);
            }
        }

    }

    /// <summary>
    /// 1. 速度調節 - 連続値
    /// 2. 高度調節 - 連続値
    /// 3. 目的地選択 - 離散値
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {
        _controller.NavigationCtrl(actions);
        
        var destination = actions.DiscreteActions[(int)NavAgentCtrlIndex.Destination];

        var isWaitingMode = destination == 0;
        var isSearchMode = destination == 1;

        if(isWaitingMode) {
            _controller.NavAgent.SetDestination(transform.position);
        } else if(isSearchMode) {
            SearchFlying(actions);
        } else  {
            Vector3 targetPos = _env.Towers[destination - 2].transform.position;
            _controller.NavAgent.SetDestination(targetPos);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        // TODO: Implement
    }

    /** Drone Event Handlers */

    /// <summary>
    /// ドローンが壁など、衝突した際のイベントハンドラー
    /// </summary>
    /// <param name="position">
    /// 衝突した位置
    /// </param>
    private void OnCrash(Vector3 position) {
        Debug.Log(LogPrefix + "Crash");
        //SetReward(-1.0f);
        SetReward(-1f);
        //エージェントグループからの登録を削除
        _env.UnregisterAgent(this.gameObject);
        gameObject.SetActive(false);
    }

    private void OnEndEpisodeHandler(float evacueeRate) {
        //誘導した避難者の割合に応じて報酬を設定
        if(guidedCount > 0) {
            AddReward(guidedCount);
        } else {
            AddReward(-1f);
        }
    }

    /** Env Event Handlers */

    /// <summary>
    /// 探索行動における飛行制御関数
    /// TODO: Nav用DroneControllerクラスができたらそっちに移管する
    /// </summary>
    private void SearchFlying(ActionBuffers actions) {
        float moveX = actions.ContinuousActions[(int)NavAgentCtrlIndex.PosX];
        float moveZ = actions.ContinuousActions[(int)NavAgentCtrlIndex.PosZ];

        Vector3 moveVector = new Vector3(moveX, 0, moveZ);

        // NavMesh上の有効なポイントを計算
        Vector3 destination = _controller.NavAgent.transform.position + moveVector * patrolRadius;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, patrolRadius, UnityEngine.AI.NavMesh.AllAreas)) {
            _controller.NavAgent.SetDestination(hit.position);
            AddReward(0.1f);

        } else {
            AddReward(-0.1f);
            //_controller.NavAgent.SetDestination(null);
        }
    }

    private void Reset() {
        //とりあえず、0地点にリセット
        transform.localRotation = Quaternion.Euler(0, 0, 0);
        transform.localPosition = StartPos;
        //Rbodyのパラメータをリセット
        _controller.Rbody.velocity = Vector3.zero;
        _controller.Rbody.useGravity = false;
        _controller.Rbody.angularVelocity = Vector3.zero;
        _controller.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _controller.batteryLevel = 100;
        currentGuidedEvacuees.Clear();
        currentGuidedEvacuees = new List<GameObject>();
        guidedCount = 0;
    }


    private List<GameObject> GetOtherAgents() {
        List<GameObject> drones = _env.Util.GetGameObjectsFromTagOnLocal(_env.gameObject, Tags.Agent);
        List<GameObject> otherAgents = new List<GameObject>();
        foreach(GameObject drone in drones) {
            if(drone != this.gameObject) {
                otherAgents.Add(drone);
            }
        }
        return otherAgents;
    }
}
