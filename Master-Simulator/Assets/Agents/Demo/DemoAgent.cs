using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DemoAgent : Agent {
    
    private DroneController _ctrl;
    void Start() {
        _ctrl = GetComponent<DroneController>();
    }
    public override void OnEpisodeBegin() {}

    /// <summary>
    /// 観測定義
    /// エージェントのXYZ回転:3
    /// エージェントのXYZ位置:3
    /// エージェントのXYZ速度:3
    /// 目標のXYZ位置:3 
    /// </summary>
    public override void CollectObservations(VectorSensor sensor) {}

    public override void OnActionReceived(ActionBuffers actions) {
        _ctrl.FlyingCtrl(actions);
    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        _ctrl.InHeuristicCtrl(actionsOut);
    }
}
