using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

/// <summary>
/// 環境に関するスクリプトのエントリポイント。Fieldオブジェクトにアタッチされる想定
/// </summary>
public class EnvManager : MonoBehaviour {

    [Header("Environment Parameters")]
    public int MaxTowerCount;
    public int MinTowerCount = 1;
    public int MinTowerCapacity = 1;
    public int MaxTowerCapacity = 10;

    [Header("GameObjects")]
    public GameObject Tower;
    public GameObject floor;

    private SimpleMultiAgentGroup Agents;
    private string LogPrefix = "EnvManager: ";

    void Start() {
        if(MinTowerCount < 0) {
            Debug.LogWarning(LogPrefix + "MinTowerCount must larger than 0");
        }
        Agents = new SimpleMultiAgentGroup();
        //TEST
        init();
    }

    /// <summary>
    /// 環境の初期化,エピソード開始時にコールされる
    /// </summary>
    public void init() {
        RegisterAgents("Agent");
        //生成する避難タワーの総数をランダムに設定
        int countTowers = Random.Range(MinTowerCount, MaxTowerCount);
        for(int i = 0; i < countTowers; i++) {
            SpawnTower();
        }
    }


    private void RegisterAgents(string agentTag) {
        GameObject[] agents = GameObject.FindGameObjectsWithTag(agentTag);
        foreach (GameObject agent in agents) {
            Agents.RegisterAgent(agent.GetComponent<Agent>());
        }
    }


    private void SpawnTower() {
        Vector3 size = floor.GetComponent<Collider>().bounds.size;
        Vector3 center = floor.transform.position;
        Vector3 randomPosition = GenerateRandomPosition(center, size);
        
        // 親のGameObjectの子としてPrefabを生成
        GameObject newObject = Instantiate(Tower, randomPosition, Quaternion.identity);
        newObject.transform.parent = transform;

        //Towerのパラメータをランダムに設定
        Tower tower = newObject.GetComponent<Tower>();
        tower.MaxCapacity = Random.Range(MinTowerCapacity, MaxTowerCapacity);
        tower.NowAccCount = 0;
    }

    private Vector3 GenerateRandomPosition(Vector3 center, Vector3 size) {
        float x = Random.Range(center.x - size.x / 2, center.x + size.x / 2);
        float z = Random.Range(center.z - size.z / 2, center.z + size.z / 2);

        // 生成されたXとZ座標を使用して新しい位置Vector3を返す
        // y座標はSubFieldPlaneのy座標に合わせるか、必要に応じて調整
        return new Vector3(x, center.y, z);
    }



}
