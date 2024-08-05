using System.Collections;
using System.Collections.Generic;
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

    public override void Start() {
        base.Start();
    }
    
    public override void InitEnv() {

        DestroyEnv();

        RegisterAgents(Tags.Agent);
        foreach(var drone in Drones) {
            drone.SetActive(true);
        }
        
        for(int i = 0; i < EvacueeSize; i++) {
            SpawnObjectOnNavMesh();
        }
        
        RegisterTowers();
    }

    private void RegisterTowers() {
        Towers.Clear();
        var towers = GameObject.FindGameObjectsWithTag(Tags.Tower);
        foreach(var tower in towers) {
            Towers.Add(tower);
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

    private void SpawnObjectOnNavMesh() {
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
        Vector3 randomPoint = transform.position + Random.insideUnitSphere * EvacueeSpawnRadius;
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
