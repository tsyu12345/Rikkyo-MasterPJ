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
    public int EvacueeSpawnMaxAttempts = 30; // 最大試行回数
    public int EvacueeSize = 100; // 避難者の数

    [Header("Loading UI")]
    public GameObject loadingPanel; // ローディング画面のPanel
    public Slider progressBar; // プログレスバー
    //public TMP_Text progressText; // 進捗を示すテキスト

    public override void Start() {
        base.Start();
        loadingPanel.SetActive(false); // 最初はローディング画面を非表示
    }
    
    public override void InitEnv() {
        DestroyEnv();

        for(int i = 0; i < EvacueeSize; i++) {
            SpawnEvacueeOnNavMesh();
        }
        RegisterTowers();
        RegisterAgents(Tags.Agent);
        foreach(var drone in Drones) {
            drone.SetActive(true);
        }
        
        // 全ての避難者のパス検索が終わるまで待機
        //StartCoroutine(WaitForAllEvacueesPathFind());
    }

    private IEnumerator WaitForAllEvacueesPathFind() {
        PathFindedEvacueeCount = 0;
        loadingPanel.SetActive(true); // ローディング画面を表示
        progressBar.value = 0; // プログレスバーをリセット

        foreach (var evacuee in Evacuees) {
            Evacuee evacueeComponent = evacuee.GetComponent<Evacuee>();

            // パス検索が完了するまで待機
            yield return new WaitUntil(() => evacueeComponent.IsPathFind);

            PathFindedEvacueeCount++;
            
            // プログレスバーを更新
            float progress = (float)PathFindedEvacueeCount / Evacuees.Count;
            progressBar.value = progress;
            // progressText.text = $"パス検索中: {(int)(progress * 100)}%";
        }

        // 全てのパス検索が完了したらローディング画面を非表示にする
        loadingPanel.SetActive(false);
        allEvacueesReady = true;
        Debug.Log("All Evacuees PathFind is done.");
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
        for (int i = 0; i < EvacueeSpawnMaxAttempts; i++) {
            Vector3 randomPoint = GetRandomPoint();
            if (TryGetNavMeshPosition(randomPoint, out Vector3 navMeshPosition)) {
                var newEvacuee = Instantiate(Evacuee, navMeshPosition, Quaternion.identity);
                Evacuees.Add(newEvacuee);
                newEvacuee.transform.parent = transform;
                newEvacuee.tag = Tags.Evacuee;
                return;
            }
        }
        Debug.LogWarning("Could not find a suitable NavMesh position after maximum attempts.");
    }

    private Vector3 GetRandomPoint() {
        Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * EvacueeSpawnRadius;
        randomPoint.y = transform.position.y; // 必要に応じて高さを調整
        return randomPoint;
    }

    private bool TryGetNavMeshPosition(Vector3 point, out Vector3 navMeshPosition) {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(point, out hit, EvacueeSpawnRadius, NavMesh.AllAreas)) {
            navMeshPosition = hit.position;
            return true;
        }

        navMeshPosition = Vector3.zero;
        return false;
    }
}
