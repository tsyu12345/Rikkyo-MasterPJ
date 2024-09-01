using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;
using Constants;

/// <summary>
/// ナビゲーションメッシュを用いたエージェントの制御クラス
/// </summary>
public class NavController : DroneController {
    public NavMeshAgent NavAgent;
    public bool isArrivalTarget = false;
    public List<GameObject> Targets = new List<GameObject>();
    public float PatrolRadius = 20f;
    public bool PathFound = false;
    private LineRenderer lineRenderer;
    void Start() {

        base.Start();

        NavAgent = GetComponent<NavMeshAgent>();
        NavAgent.autoBraking = false;

        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.positionCount = 0;
    }

    void FixedUpdate() {
        PathFound = NavAgent.pathPending? false : true;
        
        if(PathFound && NavAgent.remainingDistance <= 1.0f) {
            isArrivalTarget = true;
        } else {
            isArrivalTarget = false;
        }
    }

    public override void InHeuristicCtrl(in ActionBuffers actionsOut) {
        var action = actionsOut.ContinuousActions;

        //スロットルの調整
        if (Input.GetKey(KeyCode.UpArrow)) {
            action[(int)NavAgentCtrlIndex.Speed] += 1f;
        } else if (Input.GetKey(KeyCode.DownArrow)) {
            action[(int)NavAgentCtrlIndex.Speed] += -1f;
        }
    }
    public override void FlyingCtrl(ActionBuffers actions) {
        float speedInput = actions.ContinuousActions[(int)NavAgentCtrlIndex.Speed]; //速度の入力
        NavAgent.speed = speedInput * moveSpeed;

        var flyMode = actions.DiscreteActions[(int)NavAgentCtrlIndex.FlyMode];

        //var isWaitingMode = flyMode == 0;
        var isSearchMode = flyMode == 0;

        if(isSearchMode) {
            SearchFlying(actions);
        } else  {
            Vector3 target = Targets[(int)NavAgentCtrlIndex.Destination].transform.position;
            NavAgent.SetDestination(target);
            lineRenderer.positionCount = 0;
        }
    }

    /// <summary>
    /// 探索行動における飛行制御関数
    /// TODO: Nav用DroneControllerクラスができたらそっちに移管する
    /// </summary>
    private void SearchFlying(ActionBuffers actions) {
        float moveX = actions.ContinuousActions[(int)NavAgentCtrlIndex.PosX] * NavAgent.speed;
        float moveZ = actions.ContinuousActions[(int)NavAgentCtrlIndex.PosZ] * NavAgent.speed;

        Vector3 moveVector = new Vector3(moveX, 0, moveZ);

        // NavMesh上の有効なポイントを計算
        Vector3 destination = NavAgent.transform.position + moveVector * PatrolRadius;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, PatrolRadius, NavMesh.AllAreas)) {
            NavAgent.SetDestination(hit.position);
            // Debug用にパスを描画
            lineRenderer.positionCount = NavAgent.path.corners.Length;
            lineRenderer.SetPosition(0, transform.position);
            for (int i = 1; i < NavAgent.path.corners.Length; i++) {
                lineRenderer.SetPosition(i, NavAgent.path.corners[i]);
            }
        } else {
            // Nope
        }
    }

    /// <summary>
    /// エージェントの高度を変更する。
    /// TODO:実装中....
    /// </summary>
    /// <param name="newHeight"></param>
    /// <param name="duration"></param>
    /// <returns></returns>
    private IEnumerator NavChangeHeight(float newHeight, float duration) {
        float startHeight = NavAgent.baseOffset;
        float elapsedTime = 0f;

        while (elapsedTime < duration) {
            elapsedTime += Time.deltaTime;
            NavAgent.baseOffset = Mathf.Lerp(startHeight, newHeight, elapsedTime / duration);
            yield return null;
        }

        NavAgent.baseOffset = newHeight;
    }



}