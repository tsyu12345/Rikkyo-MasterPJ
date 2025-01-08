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
    public NavController _controller;
    public List<(float elapsedSec, string destination, float speed, int guidedCount)> actionLogs = new List<(float, string, float, int)>();

    private string LogPrefix = "DroneAgent: ";

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

        currentGuidingCount = transform.Find("GuidingCounter").GetComponent<TextMeshPro>();
        currentGoalCount = transform.Find("GuidedCounter").GetComponent<TextMeshPro>();

        _env.OnEpisodeInitialize += () => {
            _controller.Targets = _env.Towers;
            RequestDecision();
        };

        onAddEvacuee += () => {
            // AddReward(0.1f);
            //Debug.Log(LogPrefix + "Evacuee Added.");
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
        //sensor.AddObservation(FlyMode);
        sensor.AddObservation(Target == null ? Vector3.zero : Target.transform.position);
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
            sensor.AddObservation(otherAgent.currentGuidedEvacuees.Count);
            //sensor.AddObservation(otherAgent.FlyMode);
            sensor.AddObservation(otherAgent.Target == null ? Vector3.zero : otherAgent.Target.transform.position);
        }
        
        //各避難タワーからの観測情報を追加
        //観測サイズを固定しないといけないので、最大数の避難タワーを観測情報に追加(残りは空：ZeroVector, -1)
        var shelterCapacities = new List<float>();
        var shelterDistances = new List<float>();
        foreach (var towerObj in _env.Towers) {
            sensor.AddObservation(towerObj.transform.position);
            var tower = towerObj.GetComponent<Tower>();
            shelterCapacities.Add(tower.currentCapacity);
            shelterDistances.Add(Vector3.Distance(transform.position, towerObj.transform.position));
        }
        sensor.AddObservation(shelterCapacities);
        sensor.AddObservation(shelterDistances);

    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask) {
        
        // #55 : 受け入れ不可能なタワーを選択した場合、行動マスクを設定し、エージェントが選択しないようにする
        foreach (var towerObj in _env.Towers) {
            Tower tower = towerObj.GetComponent<Tower>();
            if(tower.currentCapacity <= 0) {
                actionMask.SetActionEnabled((int)NavAgentCtrlIndex.Destination, _env.Towers.IndexOf(towerObj), false);
            } else {
                // #76 キャパシティが0以上の場合、行動マスクを解除
                actionMask.SetActionEnabled((int)NavAgentCtrlIndex.Destination, _env.Towers.IndexOf(towerObj), true);
            }
        }
    }

    /// <summary>
    /// 1. 速度調節 - 連続値
    /// 2. 高度調節 - 連続値
    /// 3. 目的地選択 - 離散値
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {
        var currentTargetIdx = actions.DiscreteActions[(int)NavAgentCtrlIndex.Destination];
        var currentSpeed = ScaleAction(actions.ContinuousActions[(int)NavAgentCtrlIndex.Speed], 1, 3);

        Target = _env.Towers[currentTargetIdx];

        // #75 : 全ての避難者を誘導した場合、エピソードを終了する
        if(currentGuidedEvacuees.Count == 0) {
            Debug.Log(LogPrefix + "All Evacuees Guided. Episode End.");
            _env.UnregisterAgent(this.gameObject);
            gameObject.SetActive(false);
            return;
        }

        _controller.NavAgent.SetDestination(Target.transform.position);
        _controller.NavAgent.speed = currentSpeed;
        // #73 : エージェントの行動集計処理
        actionLogs.Add((
            _env.currentTimeSec,
            Target.name,
            _controller.NavAgent.speed, 
            currentGuidedEvacuees.Count
        ));
        //_controller.FlyingCtrl(actions);

        // #55 : 受け入れ不可能なタワーを選択した場合、負の報酬を与えてエピソードを終了（エージェント無効化）する
        Tower destinationTower = Target.GetComponent<Tower>();
        if(destinationTower.currentCapacity <= 0 && currentGuidedEvacuees.Count > 0) {
            SetReward(-1f);
            Debug.Log(LogPrefix + "Invalid Destination Tower Selected. Request New Decision.");
            //_env.UnregisterAgent(this.gameObject);
            //gameObject.SetActive(false);
            RequestDecision();
        }

        
    }


    /// <summary>
    /// 最短距離の受け入れ可能な避難タワーを選択する
    /// </summary>
    /// <param name="actionsOut"></param>
    public override void Heuristic(in ActionBuffers actionsOut) {
        // 移動速度は避難者グループの平均速度を設定
        actionsOut.ContinuousActions.Array[0] = 1.5f;

        // 目的地は最短距��の受け入れ可能な避��タワーを選択する
        // ただし、現在のタワーが空の場合は、他のタワーを��先して選択する
        // ただし、タワーが多い場合は、最も空きタワーを��先して選択する
        GameObject closestTower = GetClosestVaildShelter(transform.position);
        if(closestTower == null) {
            actionsOut.DiscreteActions.Array[0] = 0;
            return;
        }
        int index = _env.Towers.IndexOf(closestTower);
        actionsOut.DiscreteActions.Array[0] = index; // エージェントの行動を設定
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

    /// <summary>
    /// 入力座標から最も近い受け入れ可能な避難タワーを取得する #67
    /// </summary>
    /// <returns></returns>
    private GameObject GetClosestVaildShelter(Vector3 pos) {
        GameObject[] shelters = GameObject.FindGameObjectsWithTag(Tags.Tower);
        List<GameObject> sortedShelters = new List<GameObject>();
        foreach (var shelter in shelters) {
            // 受け入れ可能か判定
            Tower tower = shelter.GetComponent<Tower>();
            if (tower.currentCapacity > 0) {
                sortedShelters.Add(shelter);
            }
        }
        sortedShelters.Sort((a, b) => Vector3.Distance(a.transform.position, pos).CompareTo(Vector3.Distance(b.transform.position, pos)));
        if (sortedShelters.Count > 0) {
            return sortedShelters[0];
        } else {
            return null;
        }
    }
}
