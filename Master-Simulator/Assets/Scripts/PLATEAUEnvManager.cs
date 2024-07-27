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
    private Transform[] spawnPoints;

    public override List<GameObject> EvacueesSpawnAreas {
        get {
            return evacueesSpawnAreas;
        }
        set { 
            evacueesSpawnAreas = value;
        }
    }

    public override void Start() {
        base.Start();

        var sps = GameObject.FindGameObjectsWithTag(Tags.EvacueeSpawnArea);
        foreach (var sp in sps) {
            EvacueesSpawnAreas.Add(sp);
            spawnPoints = sp.GetComponentsInChildren<Transform>();
        }
    }
    
    public override void InitEnv() {

        DestroyEnv();

        RegisterAgents(Tags.Agent);
        foreach(var drone in Drones) {
            drone.SetActive(true);
        }
        foreach(var spawnAreaObj in EvacueesSpawnAreas) {
            var spawnArea = spawnAreaObj.GetComponent<SpawnArea>();
            SpawnEvacuees(spawnAreaObj, spawnArea.size);
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

    private void SpawnEvacuees(GameObject spawnObject, int count) {
        foreach (Transform spawnPoint in spawnPoints) {
            // ナビメッシュ上の位置にキャラクターをスポーンさせる
            NavMeshHit hit;
            if (UnityEngine.AI.NavMesh.SamplePosition(spawnPoint.position, out hit, 1.0f, UnityEngine.AI.NavMesh.AllAreas)) {
                Instantiate(Evacuee, hit.position, Quaternion.identity);
            }
        }
    }

}
