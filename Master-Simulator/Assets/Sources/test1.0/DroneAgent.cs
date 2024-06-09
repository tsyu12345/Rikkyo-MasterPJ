using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UtilityFuncs;
using Constants;

/// <summary>
/// ドローンエージェントの定義
/// </summary>
public class DroneAgent : Agent {

    public List<GameObject> guidedEvacuees = new List<GameObject>();
    public int guidedCount = 0;

    [Header("UI Elements")]
    private TextMeshPro currentGuidingCount;
    private TextMeshPro currentGoalCount;
    private DroneController _controller;
    private EnvManager _env;

    private Vector3 StartPos;

    private string LogPrefix = "DroneAgent: ";

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
    }

    void Update() {
        currentGuidingCount.text = guidedEvacuees.Count.ToString();
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
        
        //各避難タワーからの観測情報を追加
        //観測サイズを固定しないといけないので、最大数の避難タワーを観測情報に追加(残りは空：ZeroVector, -1)
        List<GameObject> towers = GetsTowers();
        int currentTowerCount = towers.Count;
        sensor.AddObservation(currentTowerCount);
        for(int i = 0; i < _env.MaxTowerCount; i++) {
            if(i < currentTowerCount) {
                sensor.AddObservation(towers[i].transform.localPosition);
                sensor.AddObservation(towers[i].GetComponent<Tower>().currentCapacity);
            } else {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(-1);
            }
        }
        //現在誘導している避難者の数を観測情報に追加
        sensor.AddObservation(guidedEvacuees.Count);
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
    /// エージェントの行動定義
    /// ・自身の飛行制御
    /// </summary>
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {
        _controller.FlyingCtrl(actions);
        // 現在誘導中の避難者の数を取得し、報酬を設定（最大１になるように）
        SetReward(((float)guidedEvacuees.Count / _env.Evacuees.Count));
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        _controller.InHeuristicCtrl(actionsOut);
    }

    /// <summary>
    /// 避難タワー毎の座標を取得する
    /// </summary>
    private List<GameObject> GetsTowers() {
        List<GameObject> towers = _env.Util.GetGameObjectsFromTagOnLocal(_env.gameObject, Tags.Tower);
        // 何故かリストがループ中に変更されてしまうので、新しいリストに追加
        List<GameObject> collectionTowers = new List<GameObject>(towers);
        foreach (GameObject tower in collectionTowers) {
            towers.Add(tower);
        }
        return towers;
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

    /** Env Event Handlers */
    
    private void OnEndEpisodeHandler(float evacueeRate) {
        //誘導した避難者の割合に応じて報酬を設定
        if(guidedCount > 0) {
            AddReward(guidedCount);
        } else {
            AddReward(-1f);
        }
    }

    private void Reset() {
        //とりあえず、0地点にリセット
        transform.localRotation = Quaternion.Euler(0, 0, 0);
        transform.localPosition = StartPos;
        //Rbodyのパラメータをリセット
        _controller.Rbody.velocity = Vector3.zero;
        _controller.Rbody.useGravity = false;
        _controller.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _controller.moveSpeed = 0;
        _controller.batteryLevel = 100;
        guidedEvacuees = new List<GameObject>();
        guidedCount = 0;
    }
}
