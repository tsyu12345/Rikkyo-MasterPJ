using System.Collections;
using System.Collections.Generic;
using System;
using Unity.MLAgents;
using UnityEngine;
using Constants;
using UtilityFuncs;
using UnityEngine.UI;
using TMPro; 

/// <summary>
/// 都市モデル環境用のスクリプト
/// </summary>
public class PLATEAUEnvManager : EnvManager {

    [SerializeField]
    private List<GameObject> SpawnAreas = new List<GameObject>();
    public override List<GameObject> EvacueesSpawnAreas {
        get { return SpawnAreas; }
        set { SpawnAreas = value; }
    }
    
    public override void InitEnv() {

        DestroyEnv();

        RegisterAgents(Tags.Agent);
        foreach(var drone in Drones) {
            drone.SetActive(true);
        }
        // 避難者キャラのスポーン
        // TODO:スポーン地点を複数用意する必要あり
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
        Debug.Log("Spawning Evacuees");
        for(int i = 0; i < count; i++) {
            SpawnObject(Evacuee, spawnObject, (evacueeObj)=> {
                evacueeObj.tag = Tags.Evacuee;
                Evacuees.Add(evacueeObj);
            });
        }
    }

}
