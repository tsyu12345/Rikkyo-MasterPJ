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
    [Header("#55 実験用パラメータ")]

    public int EvacueeSpawnSizePerDroneMin = 10;
    public int EvacueeSpawnSizePerDroneMax = 20;
    //public bool OnlyEvacueeMode = false;
    [SerializeField]
    private List<GameObject> evacueesSpawnAreas;
    [SerializeField]
    private int PathFindedEvacueeCount = 0;

    public override List<GameObject> EvacueesSpawnAreas {
        get {
            return evacueesSpawnAreas;
        }
        set { 
            evacueesSpawnAreas = value;
        }
    }

    [Header("Evacuees Spawn Settings")]
    public float EvacueeSpawnRadius = 10.0f; // ランダム生成範囲の半径
    public Vector3 SpawnCenter = Vector3.zero; // スポーンエリアの中心位置
    public int EvacueeSpawnMaxAttempts = 30; // 最大試行回数
    public int EvacueeSize = 100; // 避難者の数

    private Color gizmoColor = Color.red; // エディタ上でスポーン範囲を示す線の色

    void OnDrawGizmos() {
        Gizmos.color = gizmoColor; // Gizmoの色を設定
        var yOffset = 10.0f;
        Vector3 offsetSpawnCenter = new Vector3(SpawnCenter.x, SpawnCenter.y + yOffset, SpawnCenter.z); // Y座標をyOffset分上げる
        Gizmos.DrawWireSphere(offsetSpawnCenter, EvacueeSpawnRadius); // 中心から半径のワイヤーフレームの球体を描画
    }
    public override void Start() {
        base.Start();
        // NOTE: 一部の建物にNavMeshObstacleコンポーネントがアタッチされており、避難者が動けない場合がある問題への対処
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        foreach (var obj in allObjects) {
            if (obj.name.StartsWith("bldg_") && obj.GetComponent<NavMeshObstacle>() != null) {
                obj.GetComponent<NavMeshObstacle>().enabled = false;
            }
        }
    }
    
    public override void InitEnv() {
        DestroyEnv();
        RegisterTowers();
        // エージェントの登録
        RegisterAgents(Tags.Agent);
        Drones = new List<GameObject>(GameObject.FindGameObjectsWithTag(Tags.Agent));
        foreach(var drone in Drones) {
            // 赤円内のナビメッシュ上のランダムな位置にドローンを生成
            drone.transform.position = GetDronePosOnRandomNavMesh(); //FIXME: NavMesh上から少しずれている？ "SetDestination" can only be called on an active agent that has been placed on a NavMesh.
            
            // このドローンの直下のナビメッシュ上に避難者を生成する
            Vector3 spawnPos = drone.transform.localPosition;
            spawnPos.y = transform.position.y;
            for (int i = 0; i < UnityEngine.Random.Range(EvacueeSpawnSizePerDroneMin, EvacueeSpawnSizePerDroneMax); i++) {
                var newEvacuee = Instantiate(Evacuee, spawnPos, Quaternion.identity);
                Evacuees.Add(newEvacuee);
                newEvacuee.transform.parent = transform;
                newEvacuee.tag = Tags.Evacuee;
            }
            drone.SetActive(true);
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
            UnregisterAgent(drone);
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
}
