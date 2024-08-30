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
    public GameObject Target;
    public int FlyMode = 0;

    [Header("UI Elements")]
    private TextMeshPro currentGuidingCount;
    private TextMeshPro currentGoalCount;
    
    private EnvManager _env;
    private NavController _controller;
    private Vector3 StartPos;

    private string LogPrefix = "DroneAgent: ";

    public delegate void OnAddEvacuee();
    public OnAddEvacuee onAddEvacuee;

    void Start() {
        _controller = GetComponent<NavController>();
        _controller.PatrolRadius = patrolRadius;
        _env = GetComponentInParent<EnvManager>();
        _env.Drones.Add(this.gameObject);
        _controller.RegisterTeam(gameObject.tag);
        _controller.onCrash += OnCrash;
        _controller.onEmptyBattery += OnBatteryEmpty;
        _env.OnEndEpisode += OnEndEpisodeHandler;

        // 初期位置を保存
        StartPos = transform.localPosition;

        currentGuidingCount = transform.Find("GuidingCounter").GetComponent<TextMeshPro>();
        currentGoalCount = transform.Find("GuidedCounter").GetComponent<TextMeshPro>();

        _env.OnEpisodeInitialize += () => {
            Debug.Log("Episode Initial" + _env.Towers.Count);
            _controller.Targets = _env.Towers;
        };

        onAddEvacuee += () => {
            // AddReward(0.1f);
        };
    }

    void Update() {
        currentGuidingCount.text = currentGuidedEvacuees.Count.ToString();
        currentGoalCount.text = guidedCount.ToString();
        if(_controller.isArrivalTarget) {
            RequestDecision();
        }
    }

    public override void Initialize() {
        Debug.Log(LogPrefix + "Initialize");
        //_env.init();
    }

    public override void OnEpisodeBegin() {
        Reset();
        RequestDecision();
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
        sensor.AddObservation(FlyMode);
        sensor.AddObservation(Target == null ? Vector3.zero : Target.transform.localPosition);
        //現在誘導している避難者の数を観測情報に追加
        sensor.AddObservation(currentGuidedEvacuees.Count);

        //他のドローンの位置を観測情報に追加
        List<GameObject> otherAgents = GetOtherAgents();
        foreach(GameObject agent in otherAgents) {
            sensor.AddObservation(agent.transform.localPosition);
            // 他のドローンの選択している目的地と飛行モードを観測情報に追加
            var otherAgent = agent.GetComponent<DroneNavAgent>();
            sensor.AddObservation(otherAgent.currentGuidedEvacuees.Count);
            sensor.AddObservation(otherAgent.FlyMode);
            sensor.AddObservation(otherAgent.Target == null ? Vector3.zero : otherAgent.Target.transform.localPosition);
        }
        
        //各避難タワーからの観測情報を追加
        //観測サイズを固定しないといけないので、最大数の避難タワーを観測情報に追加(残りは空：ZeroVector, -1)
        foreach (var towerObj in _env.Towers) {
            //sensor.AddObservation(tower.transform.localPosition);
            var tower = towerObj.GetComponent<Tower>();
            sensor.AddObservation(tower.currentCapacity);
        }

    }

    /// <summary>
    /// 1. 速度調節 - 連続値
    /// 2. 高度調節 - 連続値
    /// 3. 目的地選択 - 離散値
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {
        _controller.FlyingCtrl(actions);

        var mode = actions.DiscreteActions[(int)NavAgentCtrlIndex.FlyMode];
        var currentTarget = actions.DiscreteActions[(int)NavAgentCtrlIndex.Destination];

        FlyMode = mode;
        Target = mode == 1 ? _controller.Targets[currentTarget] : null;
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

    private void OnEndEpisodeHandler(float evacueeRate) {
        if(guidedCount > 0) {
            SetReward(guidedCount);
            _env.AgentGuidedCount += guidedCount;
        } else {
            SetReward(-1f);
        }
    }

    /** Env Event Handlers */


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
        GameObject[] drones = GameObject.FindGameObjectsWithTag(Tags.Agent);
        List<GameObject> otherAgents = new List<GameObject>();
        foreach(GameObject drone in drones) {
            if(drone != this.gameObject) {
                otherAgents.Add(drone);
            }
        }
        return otherAgents;
    }
}
