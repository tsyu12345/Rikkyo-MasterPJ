using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using UnityEngine.AI;
using UtilityFuncs;
using Constants;

public class DroneNavSearchAgent : Agent {
    
    [Header("Agent Parameters")]
    public float patrolRadius = 10f;
    public List<GameObject> currentGuidedEvacuees = new List<GameObject>();
    public int guidedCount = 0;

    [Header("UI Elements")]
    private TextMeshPro currentGuidingCount;
    private TextMeshPro currentGoalCount;
    
    private EnvManager _env;
    public NavController _controller;
    public List<(float elapsedSec, float moveX, float moveZ, float speed)> actionLogs = new List<(float, float, float, float)>();

    private string LogPrefix = "DroneSearchAgent: ";

    public delegate void OnAddEvacuee();
    public OnAddEvacuee onAddEvacuee;

    void Start() {
        _controller = GetComponent<NavController>();
        _controller.PatrolRadius = patrolRadius;
        _env = GetComponentInParent<EnvManager>();
        _env.Drones.Add(this.gameObject);

        if(_env.SimulateMode == EnvManager.SimulateModeSetting.EvacueesOnly) {
            this.gameObject.SetActive(false);
            return;
        }

        _controller.RegisterTeam(gameObject.tag);
        _controller.onCrash += OnCrash;
        _controller.onEmptyBattery += OnBatteryEmpty;
        //_env.OnEndEpisode += OnEndEpisodeHandler;
        /*
        currentGuidingCount = transform.Find("GuidingCounter").GetComponent<TextMeshPro>();
        currentGoalCount = transform.Find("GuidedCounter").GetComponent<TextMeshPro>();
        */
        _env.OnEpisodeInitialize += () => {
            _controller.Targets = _env.Towers;
            RequestDecision();
        };

        onAddEvacuee += () => {
            AddReward(1f);
            //Debug.Log(LogPrefix + "Evacuee Added.");
        };
    }

    void Update() {
        /*
        currentGuidingCount.text = currentGuidedEvacuees.Count.ToString();
        currentGoalCount.text = guidedCount.ToString();
        */
        
        if(_controller.isArrivalTarget) {
            RequestDecision();
        }
    }

    public override void Initialize() {
        Debug.Log(LogPrefix + "Initialize");
        //_env.init();
    }

    public override void OnEpisodeBegin() {
        //Reset();
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
        sensor.AddObservation(transform.position);
        sensor.AddObservation(_controller.NavAgent.speed);
        
        //現在誘導している避難者の数, 移動速度平均を観測情報に追加
        sensor.AddObservation(currentGuidedEvacuees.Count); //FIXME : 初回の環境観測が正常に行えていない
        float sumSpeed = 0.0f;
        foreach(GameObject evacuee in currentGuidedEvacuees) {
            var evacueeComp = evacuee.GetComponent<Evacuee>();
            sumSpeed += evacueeComp.Speed;
        }
        float avgSpeed = 0.0f;
        if(currentGuidedEvacuees.Count > 0) {
            avgSpeed = sumSpeed / currentGuidedEvacuees.Count;
        }
        sensor.AddObservation(avgSpeed);
        
        // #66 制限時間を観測情報に追加
        sensor.AddObservation(_env.LimitTimeSec);
        sensor.AddObservation(_env.currentTimeSec);

        //他のドローンの位置を観測情報に追加
        List<GameObject> otherAgents = GetOtherAgents();
        foreach(GameObject agent in otherAgents) {
            sensor.AddObservation(agent.transform.position);
            // 他のドローンの選択している目的地と飛行モードを観測情報に追加
            var otherAgent = agent.GetComponent<DroneNavAgent>();
            //sensor.AddObservation(otherAgent.currentGuidedEvacuees.Count);
        }

    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask) {
       
    }

    /// <summary>
    /// （連続値）現在位置からの変化量 X, Z
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {
        /*
        var currentTargetIdx = actions.DiscreteActions[(int)NavAgentCtrlIndex.Destination];
        var currentSpeed = ScaleAction(actions.ContinuousActions[(int)NavAgentCtrlIndex.Speed], 1, 3);
        */
        var movementX = ScaleAction(actions.ContinuousActions[0], -10, 10);
        var movementZ = ScaleAction(actions.ContinuousActions[1], -10, 10);
        var speed = ScaleAction(actions.ContinuousActions[2], 1, 3);

        Vector3 offset = new Vector3(movementX, transform.position.y, movementZ);
        _controller.MoveAgent(offset, speed);

        // #73 : エージェントの行動集計処理
        actionLogs.Add((
            _env.currentTimeSec,
            movementX,
            movementZ,
            speed
        ));

        
    }


    /// <summary>
    /// 最短距離の受け入れ可能な避難タワーを選択する
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut) {
        
    }

    /** Drone Event Handlers */

    /// <summary>
    /// ドローンが壁など、衝突した際のイベントハンドラー
    /// </summary>
    /// <param name="position">
    /// 衝突した位置
    /// </param>
    private void OnCrash(Vector3 position) {
        //SetReward(-1.0f);
        SetReward(-1f);
        //エージェントグループからの登録を削除
        _env.UnregisterAgent(this.gameObject);
        gameObject.SetActive(false);
    }

    private void OnBatteryEmpty() {
        // TODO:ドローンの充電ステーションを加えてみる
        //エージェントグループからの登録を削除
        _env.UnregisterAgent(this.gameObject);
        gameObject.SetActive(false);
    }


    public void Reset() {
        //とりあえず、0地点にリセット
        transform.localRotation = Quaternion.Euler(0, 0, 0);
        //transform.localPosition = StartPos;
        //Rbodyのパラメータをリセット
        _controller.Rbody.velocity = Vector3.zero;
        _controller.Rbody.useGravity = false;
        _controller.Rbody.angularVelocity = Vector3.zero;
        _controller.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _controller.batteryLevel = 100;
        currentGuidedEvacuees.Clear();
        currentGuidedEvacuees = new List<GameObject>();
        actionLogs.Clear();
        guidedCount = 0;
        this.gameObject.SetActive(true);
    }


    private List<GameObject> GetOtherAgents() {
        List<GameObject> otherAgents = new List<GameObject>();
        foreach(GameObject drone in _env.Drones) {
            if(drone != this.gameObject) {
                otherAgents.Add(drone);
            }
        }
        return otherAgents;
    }

}
