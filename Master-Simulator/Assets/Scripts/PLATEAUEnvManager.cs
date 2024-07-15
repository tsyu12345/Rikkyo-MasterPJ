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
    
    public override void InitEnv() {

        DestoryEnv();

        RegisterAgents(Tags.Agent);
        foreach(var drone in Drones) {
            drone.SetActive(true);
        }
        // 避難者キャラのスポーン
        // TODO:スポーン地点を複数用意する必要あり
        int countEvacuees = UnityEngine.Random.Range(MinEvacueeCount, MaxEvacueeCount);
        for(int i = 0; i < countEvacuees; i++) {
            SpawnObjects(Evacuee, EvacueesSpawn, (evacueeObj)=> {
                evacueeObj.tag = Tags.Evacuee;
                Evacuees.Add(evacueeObj);
            });
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

    private void DestoryEnv() {
        RemoveObjectAll(Tags.Evacuee);
        foreach(var drone in Drones) {
           UnregisterAgent(drone);
        }

        Evacuees.Clear();
        m_ResetTimer = 0;
        AgentGuidedCount = 0;
        Evacuees = new List<GameObject>();
    }

}
