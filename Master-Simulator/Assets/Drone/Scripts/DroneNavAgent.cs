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
    public int currentTowerIndex = -1;
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
        _env.Drones.Add(gameObject);
        _controller.RegisterTeam(gameObject.tag);
        _controller.onCrash += OnCrash;
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
        //自身の位置・速度・飛行モード・選択しているタワーのindex・タワーまでの距離を観測情報に追加
        sensor.AddObservation(transform.position);
        sensor.AddObservation(_controller.NavAgent.speed);
        sensor.AddObservation(FlyMode);
        sensor.AddObservation(currentTowerIndex);
        if(Target != null) {
            sensor.AddObservation(Vector3.Distance(transform.position, Target.transform.position));
        } else {
            sensor.AddObservation(-1f);
        }
        //現在誘導している避難者の数を観測情報に追加
        sensor.AddObservation(currentGuidedEvacuees.Count);
        //ゴール済みの避難者の数を観測情報に追加
        sensor.AddObservation(guidedCount);

        //他のドローンの位置を観測情報に追加
        List<GameObject> otherAgents = GetOtherAgents();
        foreach(GameObject agent in otherAgents) {
            sensor.AddObservation(agent.transform.position);
            var otherAgent = agent.GetComponent<DroneNavAgent>();
            List<float> info = new List<float>();
            info.Add((float)otherAgent.currentGuidedEvacuees.Count);
            info.Add((float)otherAgent.FlyMode);
            info.Add((float)otherAgent.currentTowerIndex);
            sensor.AddObservation(info);
        }
        
        //各避難タワーからの観測情報を追加
        var currentTowerCapacity = new List<float>();
        foreach (var towerObj in _env.Towers) {
            //sensor.AddObservation(tower.transform.localPosition);
            var tower = towerObj.GetComponent<Tower>();
            currentTowerCapacity.Add((float)tower.currentCapacity);
        }
        sensor.AddObservation(currentTowerCapacity);

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
        Target = mode == 2 ? _controller.Targets[currentTarget] : null;
        currentTowerIndex = mode == 2 ? currentTarget : -1;

        //#46 1度目のタワー到達 + 誘導後にまだか抱えている避難者がいる場合、有効なタワー選択をしなかったら個別のエージェントに対し負の報酬を与えてみる。
        if(mode == 2) {
            //選択したタワーのカレントのキャパを取得し、0以下の場合は負の報酬を与える
            var tower = _controller.Targets[currentTarget].GetComponent<Tower>();
            if(tower.currentCapacity <= 0) {
                SetReward(-1f);
            }
        } else {
            // 誘導中の避難者がいて、タワーが選択されていない場合、負の報酬を与える
            if(currentGuidedEvacuees.Count > 0) {
                SetReward(-1f);
            }
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
        //SetReward(-1.0f);
        SetReward(-1f);
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
