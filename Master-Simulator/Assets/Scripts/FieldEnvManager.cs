using System.Collections;
using System.Collections.Generic;
using System;
using Unity.MLAgents;
using UnityEngine;
using Constants;
using UtilityFuncs;
using UnityEngine.UI;
using TMPro; 

public class FieldEnvManager : EnvManager {

    /** 避難タワーの設定*/
    public int MaxTowerCount;
    public int MinTowerCount = 1;
    public int MinTowerCapacity = 1;
    public int MaxTowerCapacity = 10;
    /** エージェントの設定*/
    public int MaxAgentCount = 1;
    public int MinAgentCount = 1;
    /* 障害物の設定 */
    public int MaxObstacleCount = 1;
    public int MinObstacleCount = 1;
    [Header("GameObjects")]
    public GameObject Tower;
    public GameObject TowerSpawn;
    public GameObject ObstacleWall;
    public GameObject Aisle;
    
    public override void InitEnv() {
        RemoveObjectAll(Tags.Tower);
        RemoveObjectAll(Tags.Evacuee);
        RemoveObjectAll(Tags.Obstacle);
        foreach(var drone in Drones) {
           UnregisterAgent(drone);
        }
        Towers.Clear();
        Evacuees.Clear();

        m_ResetTimer = 0;
        AgentGuidedCount = 0;
        Evacuees = new List<GameObject>();
        Towers = new List<GameObject>();
        RegisterAgents(Tags.Agent);
        foreach(var drone in Drones) {
            drone.SetActive(true);
        }
        
        // タワーのスポーン
        int countTowers = UnityEngine.Random.Range(MinTowerCount, MaxTowerCount);
        for(int i = 0; i < countTowers; i++) {
            SpawnObjects(Tower, TowerSpawn, (towerObj)=> {
                //Towerのパラメータをランダムに設定
                Tower tower = towerObj.GetComponent<Tower>();
                tower.MaxCapacity = UnityEngine.Random.Range(MinTowerCapacity, MaxTowerCapacity);
                tower.NowAccCount = 0;
                tower.uuid = Guid.NewGuid().ToString();
                towerObj.tag = Tags.Tower;
                Towers.Add(towerObj);
            });
        }

        // 避難者キャラのスポーン
        int countEvacuees = UnityEngine.Random.Range(MinEvacueeCount, MaxEvacueeCount);
        for(int i = 0; i < countEvacuees; i++) {
            SpawnObjects(Evacuee, EvacueesSpawn, (evacueeObj)=> {
                evacueeObj.tag = Tags.Evacuee;
                Evacuees.Add(evacueeObj);
            });
        }

        // 障害物のスポーン
        int countObstacles = UnityEngine.Random.Range(MinObstacleCount, MaxObstacleCount);
        for(int i = 0; i < countObstacles; i++) {
            SpawnObjects(ObstacleWall, Aisle, (obstacleObj)=> {
                obstacleObj.tag = Tags.Obstacle;
            });
        }
    }
}
