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

    [Header("GameObjects")]
    public GameObject Tower;

    private SimpleMultiAgentGroup Agents;
    private string LogPrefix = "EnvManager: ";

    void Start() {
        if(MinTowerCount < 0) {
            Debug.LogWarning(LogPrefix + "MinTowerCount must larger than 0");
        }
        Agents = new SimpleMultiAgentGroup();
        RegisterAgents("Agent");
    }

    /// <summary>
    /// 環境の初期化,エピソード開始時にコールされる
    /// </summary>
    public void init() {
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
        //Fieldの範囲を取得
        Bounds bounds = gameObject.GetComponent<Renderer>().bounds;
        // 親の範囲内でランダムな位置を計算（y座標は固定）
        float randomX = Random.Range(bounds.min.x, bounds.max.x);
        float fixedY = bounds.center.y; // 固定されたy座標
        float randomZ = Random.Range(bounds.min.z, bounds.max.z);
        Vector3 randomPosition = new Vector3(randomX, fixedY, randomZ);

        // 親のGameObjectの子としてPrefabを生成
        GameObject newObject = Instantiate(Tower, randomPosition, Quaternion.identity);
        newObject.transform.parent = transform;
    }



}
