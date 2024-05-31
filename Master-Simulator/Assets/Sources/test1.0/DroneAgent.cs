using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    private DroneController _controller;
    private EnvManager _env;

    private string LogPrefix = "DroneAgent: ";


    void Start() {
        _controller = GetComponent<DroneController>();
        _env = GetComponentInParent<EnvManager>();
        _controller.RegisterTeam(gameObject.tag);
        _controller.onCrash += OnCrash;
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
    /// ２．避難所の位置 Vector3
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor) {
        //自身の位置・速度を観測情報に追加
        sensor.AddObservation(transform.localPosition);
        sensor.AddObservation(_controller.Rbody.velocity);
        //各避難タワーからの観測情報を追加
        //観測サイズを固定しないといけないので、最大数の避難タワーを観測情報に追加(残りは空：ZeroVector, 0)
        List<GameObject> towers = GetsTowers();
        int currentTowerCount = towers.Count;
        sensor.AddObservation(currentTowerCount);
        for(int i = 0; i < _env.MaxTowerCount; i++) {
            if(i < currentTowerCount) {
                sensor.AddObservation(towers[i].transform.localPosition);
                sensor.AddObservation(towers[i].GetComponent<Tower>().currentCapacity);
            } else {
                sensor.AddObservation(Vector3.zero);
                sensor.AddObservation(0);
            }
        }
        //現在誘導している避難者の数を観測情報に追加
        sensor.AddObservation(guidedEvacuees.Count);

    }

    /// <summary>
    /// エージェントの行動定義
    /// ・自身の飛行制御
    /// </summary>
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {
        _controller.FlyingCtrl(actions);
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
        EndEpisode();
    }

    private void Reset() {
        //transform.localPosition = StartPosition;
        //transform.localRotation = Quaternion.Euler(0, 0, 0);
        //とりあえず、0地点にリセット
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(0, 0, 0);
        _controller.batteryLevel = 100;
        _controller.Rbody.velocity = Vector3.zero;
        _controller.Rbody.useGravity = false;
        //X,Z回転を固定
        _controller.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        guidedEvacuees = new List<GameObject>();
    }
}
