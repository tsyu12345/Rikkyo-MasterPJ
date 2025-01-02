using System.Collections;
using System.Collections.Generic;
using System;
using Unity.MLAgents;
using UnityEngine;
using Constants;
using UtilityFuncs;
using UnityEngine.UI;
using UnityEngine.AI;
using TMPro;

/// <summary>
/// 都市モデル環境用のスクリプト
/// </summary>
public class PLATEAUEnvManager : EnvManager {

    [Header("Evacuees Spawn Settings")]
    public float EvacueeSpawnRadius = 10.0f; // ランダム生成範囲の半径
    public Vector3 SpawnCenter = Vector3.zero; // スポーンエリアの中心位置
    public int EvacueeSize = 100; // 避難者の数

    private Color gizmoColor = Color.red; // エディタ上でスポーン範囲を示す線の色

    void OnDrawGizmos() {
        Gizmos.color = gizmoColor;
        DrawWireCircle(SpawnCenter, EvacueeSpawnRadius);
    }
    public override void Start() {
        base.Start();
        // NOTE: 一部の建物にNavMeshObstacleコンポーネントがアタッチされており、避難者が動けない場合がある問題への対処
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects) {
            if (obj.name.StartsWith("bldg_") && obj.GetComponent<NavMeshObstacle>() != null) {
                obj.GetComponent<NavMeshObstacle>().enabled = false;
            } else if(obj.CompareTag("TowerBldg") || obj.CompareTag("Tower")) {
                obj.GetComponent<NavMeshObstacle>().enabled = false;
            }
        }
    }
    
    public override void InitEnv() {
        DestroyEnv();
        RegisterTowers();

        // 避難者のみモード & 避難者の初期位置が各個ランダム
        if(base.SimulateMode == SimulateModeSetting.EvacueesOnly && base.EvacueeSpawnPosMode == EvacueeSpawnModeSetting.SingleRandom) {
            for(int i = 0; i < EvacueeSize; i++) {
                // 赤円内のナビメッシュ上のランダムな位置に避難者を生成
                SpawnEvacueeOnNavMesh();
            }
        // 避難者のみモード & 避難者の初期位置がグループ毎ランダムの場合
        } else if(base.SimulateMode == SimulateModeSetting.EvacueesOnly && base.EvacueeSpawnPosMode == EvacueeSpawnModeSetting.Group) {
            // 仮想的にエージェントの生成位置を計算し、その配下に避難者グループを形成させる。
            foreach(var drone in base.Drones) {
                var virtualDronePos = GetDronePosOnRandomNavMesh();
                Vector3 spawnPos = virtualDronePos;
                spawnPos.y = 1.5f; // 避難者の高さを設定
                for (int i = 0; i < UnityEngine.Random.Range(EvacueeSpawnSizePerDroneMin, EvacueeSpawnSizePerDroneMax); i++) {
                    var newEvacuee = Instantiate(Evacuee, spawnPos, Quaternion.identity, transform);
                    Evacuee evacueeIns = newEvacuee.GetComponent<Evacuee>();
                    var isCompleteWarp = evacueeIns.navMeshAgent.Warp(spawnPos);
                    if (!isCompleteWarp) {
                        Debug.LogError("Failed to warp evacuee to drone position");
                        // Sampleを使って再度位置を取得
                        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)) {
                            newEvacuee.transform.position = hit.position;
                        } else {
                            Debug.LogWarning("避難者がNavMesh上に生成できませんでした。位置を確認してください。");
                        }
                    }
                    Evacuees.Add(newEvacuee);
                    newEvacuee.tag = Tags.Evacuee;
                    Vector3 CalibrationPos = new Vector3(spawnPos.x, spawnPos.y, spawnPos.z);
                    CalibrationPos = newEvacuee.transform.position;
                }
            }
        // エージェント有りモード
        } else {
            RegisterAgents(Tags.Agent);
            Drones = new List<GameObject>(GameObject.FindGameObjectsWithTag(Tags.Agent));
            foreach(var drone in Drones) {
                // 赤円内のナビメッシュ上のランダムな位置にドローンを生成
                drone.transform.position = GetDronePosOnRandomNavMesh();
                NavController agentController = drone.GetComponent<NavController>();
                agentController.NavAgent.radius = 1.0f;
                // このドローンの直下のナビメッシュ上に避難者を生成する
                Vector3 spawnPos = drone.transform.position;
                spawnPos.y = 1.5f; // 避難者の高さを設定
                Vector3 CalibrationPos = new Vector3(spawnPos.x, spawnPos.y, spawnPos.z);
                for (int i = 0; i < UnityEngine.Random.Range(EvacueeSpawnSizePerDroneMin, EvacueeSpawnSizePerDroneMax); i++) {
                    var newEvacuee = Instantiate(Evacuee, spawnPos, Quaternion.identity, transform);
                    Evacuee evacueeIns = newEvacuee.GetComponent<Evacuee>();
                    var isCompleteWarp = evacueeIns.navMeshAgent.Warp(spawnPos);
                    if (!isCompleteWarp) {
                        Debug.LogError("Failed to warp evacuee to drone position");
                        // Sampleを使って再度位置を取得
                        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 1.0f, NavMesh.AllAreas)) {
                            newEvacuee.transform.position = hit.position;
                        } else {
                            Debug.LogError("避難者がNavMesh上に生成できませんでした。位置を確認してください。");
                        }
                    }
                    Evacuees.Add(newEvacuee);
                    newEvacuee.tag = Tags.Evacuee;
                    CalibrationPos = newEvacuee.transform.position;
                }
                drone.SetActive(true);
                // NOTE: #60-何故か生成後にエージェントの位置が避難者のところにいないことがあるので、ここで補正する。
                // スポーンした避難者の位置にドローンを移動させる
                agentController.NavAgent.Warp(CalibrationPos);
            }
        }

        // 避難者の移動速度の設定
        foreach (var evacuee in Evacuees) {
            var eva = evacuee.GetComponent<Evacuee>();
            if (base.EvacueeSpeedMode == EvacueeSpeedSetting.Random) {
                eva.Speed = UnityEngine.Random.Range(base.EvacueeSpeedMin, base.EvacueeSpeedMax);
            } else {
                eva.Speed = base.ConstantEvacueeSpeed;
            }
        }
        
    }


    private void RegisterTowers() {
        Towers.Clear();
        var towers = GameObject.FindGameObjectsWithTag(Tags.Tower);
        foreach(var towerObj in towers) {
            Tower tower = towerObj.GetComponent<Tower>();
            tower.uuid = Guid.NewGuid().ToString();
            Towers.Add(towerObj);
        }
    }

    private void DestroyEnv() {
        RemoveObjectAll(Tags.Evacuee);
        
        foreach(var drone in Drones) {
            if(base.SimulateMode == SimulateModeSetting.AgentsModel) {
                UnregisterAgent(drone);
            }
        }

        Evacuees.Clear();
        m_ResetTimer = 0;
        AgentGuidedCount = 0;
        Evacuees = new List<GameObject>();
    }

    private void SpawnEvacueeOnNavMesh() {
        var spawnPos = GetRandomPositionOnNavMesh();
        spawnPos.y = transform.position.y;
        var newEvacuee = Instantiate(Evacuee, spawnPos, Quaternion.identity);
        Evacuees.Add(newEvacuee);
        newEvacuee.transform.parent = transform;
        newEvacuee.tag = Tags.Evacuee;
    }

    private Vector3 GetDronePosOnRandomNavMesh() {
        var spawnPos = GetRandomPositionOnNavMesh();
        spawnPos.y = transform.position.y;
        return spawnPos;
    }

    /// <summary>
    /// ナビメッシュ上の任意の座標を取得する。
    /// </summary>
    /// <returns>ランダムなナビメッシュ上の座標 or Vector3.zero</returns>
    private Vector3 GetRandomPositionOnNavMesh() {
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * EvacueeSpawnRadius; // 半径内のランダムな位置を取得
        randomDirection += SpawnCenter; // 中心位置を加算
        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, EvacueeSpawnRadius, NavMesh.AllAreas)) {
            return hit.position;
        }
        return Vector3.zero; // ナビメッシュが見つからなかった場合
    }

    /// <summary>
    /// 避難者のランダムスポーン範囲を描画する
    /// </summary>
    private static void DrawWireCircle(Vector3 center, float radius, int segments = 36) {
        float angle = 0f;
        float angleStep = 360f / segments;

        Vector3 prevPoint = center + new Vector3(radius, 5, 0); // 初期点

        for (int i = 1; i <= segments; i++) {
            angle += angleStep;
            float rad = Mathf.Deg2Rad * angle;

            Vector3 newPoint = center + new Vector3(Mathf.Cos(rad) * radius, 5, Mathf.Sin(rad) * radius);
            Gizmos.DrawLine(prevPoint, newPoint);

            prevPoint = newPoint; // 次の線を描画するために現在の点を更新
        }
    }
}
