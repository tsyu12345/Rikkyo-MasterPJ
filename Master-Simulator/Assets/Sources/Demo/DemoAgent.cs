using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// テスト用エージェント
/// ドローンの制御と単純なターゲット追跡タスク 
/// </summary>
public class DemoAgent : Agent {

    [Header("Goal Target")]
    public GameObject Target;
    [Header("Field Object")]
    public GameObject Field;

    private DroneController _ctrl;
    private DemoEnvManager _envManager;
    private Vector3 startPos;
    void Start() {
        _ctrl = GetComponent<DroneController>();
        _envManager = Field.GetComponent<DemoEnvManager>();

        //Add Listeners
        _ctrl.onCrash += (Vector3 crashPos) => {
            EndEpisode();
        };
        startPos = transform.localPosition;
    }

    void OnTriggerEnter(Collider other) {
        if (other.tag == "Balloon") {
            AddReward(1.0f);
            EndEpisode();
        }
    }
    public override void OnEpisodeBegin() {
        //位置、回転速度のリセット
        transform.localPosition = startPos;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        _ctrl.Rbody.velocity = Vector3.zero;
        _ctrl.Rbody.useGravity = false;
        _ctrl.Rbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        _ctrl.batteryLevel = 100f;

        //new target Pos
        Vector3 targetPos = new Vector3(
            Random.Range(-10f, 10f),
            Random.Range(1f, 15f),
            Random.Range(-10f, 10f)
        );
        Target.transform.localPosition = targetPos;
    }

    /// <summary>
    /// 観測定義
    /// エージェントのXYZ回転:3
    /// エージェントのXYZ位置:3
    /// エージェントのXYZ速度:3
    /// 目標のXYZ位置:3 
    /// </summary>
    public override void CollectObservations(VectorSensor sensor) {
        sensor.AddObservation(transform.rotation);
        sensor.AddObservation(transform.position);
        sensor.AddObservation(_ctrl.Rbody.velocity);
        sensor.AddObservation(Target.transform.position);
    }

    public override void OnActionReceived(ActionBuffers actions) {
        _ctrl.FlyingCtrl(actions);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        _ctrl.InHeuristicCtrl(actionsOut);
    }
}
