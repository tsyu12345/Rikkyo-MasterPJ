using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

/// <summary>
/// ドローンエージェントの定義
/// </summary>
public class DroneAgent : Agent {
    private DroneController _controller;
    private EnvManager _env;


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
    /// ２．偵察して得た避難所の位置 Vector3
    /// </summary>
    /// <param name="sensor"></param>
    public override void CollectObservations(VectorSensor sensor) {}

    /// <summary>
    /// エージェントの行動定義
    /// </summary>
    /// <param name="actions"></param>
    public override void OnActionReceived(ActionBuffers actions) {}

    public override void Heuristic(in ActionBuffers actionsOut) {
        _controller.InHeuristicCtrl(actionsOut);
    }

    private void Reset() {
        //transform.localPosition = StartPosition;
        //transform.localRotation = Quaternion.Euler(0, 0, 0);
        _controller.batteryLevel = 100;
        _controller.Rbody.velocity = Vector3.zero;
        _controller.Rbody.useGravity = false;
        //X,Z回転を固定
        _controller.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }
}
