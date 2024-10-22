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
    public float[] EvacueeSpawnSizeRangePaeDrone = { 10.0f, 20.0f }; // ドローン1台あたりの避難者の数の範囲
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

    [Header("Loading UI")]
    public GameObject loadingPanel; // ローディング画面のPanel
    public Slider progressBar; // プログレスバー
    //public TMP_Text progressText; // 進捗を示すテキスト

    private Color gizmoColor = Color.red; // エディタ上でスポーン範囲を示す線の色

    void OnDrawGizmos() {
        Gizmos.color = gizmoColor; // Gizmoの色を設定
        var yOffset = 10.0f;
        Vector3 offsetSpawnCenter = new Vector3(SpawnCenter.x, SpawnCenter.y + yOffset, SpawnCenter.z); // Y座標をyOffset分上げる
        Gizmos.DrawWireSphere(offsetSpawnCenter, EvacueeSpawnRadius); // 中心から半径のワイヤーフレームの球体を描画
    }
    public override void Start() {
        base.Start();
        loadingPanel.SetActive(false); // 最初はローディング画面を非表示
    }
    
    public override void InitEnv() {
        DestroyEnv();
        /*
        for(int i = 0; i < EvacueeSize; i++) {
            SpawnEvacueeOnNavMesh();
        }
        */
        RegisterTowers();
        RegisterAgents(Tags.Agent);
        // TODO;ナビメッシュ上の有効なポイントにエージェントの位置を設定する
        foreach(var drone in Drones) {
            drone.transform.localPosition = GetDronePosOnRandomNavMesh(drone);
            // TODO: 指定した範囲で、ドローンの直下のナビメッシュ上に避難者を生成する
            Vector3 spawnPos = drone.transform.localPosition;
            spawnPos.y = transform.position.y;
            var newEvacuee = Instantiate(Evacuee, spawnPos, Quaternion.identity);
            Evacuees.Add(newEvacuee);
            newEvacuee.transform.parent = transform;
            newEvacuee.tag = Tags.Evacuee;
            
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

    private Vector3 GetDronePosOnRandomNavMesh(GameObject drone) {
        var spawnPos = GetRandomPositionOnNavMesh();
        spawnPos.y = drone.transform.position.y;
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
