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

    private Utils Utils = new Utils();


    void Start() {
        _controller = GetComponent<DroneController>();
        _env = GetComponentInParent<EnvManager>();
        _controller.RegisterTeam(gameObject.tag);
    }

    public override void OnEpisodeBegin() {
        //_env.InitializeRandomPositions();
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
        List<GameObject> towers = GetsTowers();
        foreach (GameObject tower in towers) {
            sensor.AddObservation(tower.transform.localPosition);
            sensor.AddObservation(tower.GetComponent<Tower>().currentCapacity);
        }
        //現在誘導している避難者の数を観測情報に追加
        //sensor.AddObservation(guidedCount);

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
        List<GameObject> towers = Utils.GetGameObjectsFromTagOnLocal(_env.gameObject, Tags.Tower);
        foreach (GameObject tower in towers) {
            towers.Add(tower);
        }
        return towers;
    }

    private void Reset() {
        //transform.localPosition = StartPosition;
        //transform.localRotation = Quaternion.Euler(0, 0, 0);
        _controller.batteryLevel = 100;
        _controller.Rbody.velocity = Vector3.zero;
        _controller.Rbody.useGravity = false;
        //X,Z回転を固定
        _controller.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        guidedEvacuees = new List<GameObject>();
    }
}
