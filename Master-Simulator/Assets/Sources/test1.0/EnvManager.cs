using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using Constants;
using UtilityFuncs;
using UnityEngine.UI;
using TMPro; 

/// <summary>
/// 環境に関するスクリプトのエントリポイント。Fieldオブジェクトにアタッチされる想定
/// TODO:Spawn処理を共通化する
/// </summary>
public class EnvManager : MonoBehaviour {

    [Header("Environment Parameters")]
    public float EvacuationRate = 0.0f;
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 1000; 
    /** 避難タワーの設定*/
    public int MaxTowerCount;
    public int MinTowerCount = 1;
    public int MinTowerCapacity = 1;
    public int MaxTowerCapacity = 10;
    /**避難者の設定*/
    public int MinEvacueeCount = 1;
    public int MaxEvacueeCount = 10;
    /** エージェントの設定*/
    public int MaxAgentCount = 1;
    public int MinAgentCount = 1;

    [Header("GameObjects")]
    public GameObject Tower;
    public GameObject Evacuee;
    public GameObject EvacueesSpawn;
    public GameObject Agent;
    public List<GameObject> Evacuees;
    public List<GameObject> Towers;
    public GameObject floor;
    
    [Header("UI Elements")]
    public TextMeshProUGUI stepCounter;
    public TextMeshProUGUI evacRateCounter;

    public Utils Util;

    public delegate void EvacueeAllHandler();
    public EvacueeAllHandler OnEvacueeAll;
    private SimpleMultiAgentGroup Agents;
    private string LogPrefix = "EnvManager: ";
    private int m_ResetTimer;

    void Start() {
        if(MinTowerCount < 0) {
            Debug.LogWarning(LogPrefix + "MinTowerCount must larger than 0");
        }
        Agents = new SimpleMultiAgentGroup();
        Util = GetComponent<Utils>();
        RegisterAgents("Agent");
        init();
    }

    void FixedUpdate() {
        m_ResetTimer += 1;
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0) {
            Agents.GroupEpisodeInterrupted();
            init();
        }
        if (isEvacueeAll()) {
            OnEvacueeAll?.Invoke();
            OnEvacueeAllHandler();
        }
        EvacuationRate = CalcEvacuationRate();
        UpdateUI();
    }

    /// <summary>
    /// 環境の初期化,全体エピソード開始時にコールされる
    /// </summary>
    public void init() {
        RemoveAllTowers();
        RemoveAllEvacuees();
        m_ResetTimer = 0;
        Evacuees = new List<GameObject>();
        Towers = new List<GameObject>();
        /*
        int countAgents = UnityEngine.Random.Range(MinAgentCount, MaxAgentCount);
        for(int i = 0; i < countAgents; i++) {
            SpawnAgent();
        }
        */

        int countTowers = UnityEngine.Random.Range(MinTowerCount, MaxTowerCount);
        for(int i = 0; i < countTowers; i++) {
            SpawnTower();
        }
        int countEvacuees = UnityEngine.Random.Range(MinEvacueeCount, MaxEvacueeCount);
        for(int i = 0; i < countEvacuees; i++) {
            SpawnEvacuee();
        }
    }


    private void RegisterAgents(string agentTag) {
        GameObject[] agents = GameObject.FindGameObjectsWithTag(Tags.Agent);
        foreach (GameObject agent in agents) {
            Agents.RegisterAgent(agent.GetComponent<Agent>());
        }
    }

    private void SpawnAgent() {    
        Vector3 size = floor.GetComponent<Collider>().bounds.size;
        Vector3 center = floor.transform.position;
        Vector3 randomPosition = GenerateRandomPosition(center, size);
        
        GameObject newObject = Instantiate(Evacuee, randomPosition, Quaternion.identity);
        newObject.transform.parent = transform;
        newObject.tag = Tags.Agent;
    }


    private void SpawnTower() {
        Vector3 size = floor.GetComponent<Collider>().bounds.size;
        Vector3 center = floor.transform.position;
        Vector3 randomPosition = GenerateRandomPosition(center, size);
        
        // 親のGameObjectの子としてPrefabを生成
        GameObject newObject = Instantiate(Tower, randomPosition, Quaternion.identity);
        newObject.transform.parent = transform;
        newObject.tag = Tags.Tower;

        //Towerのパラメータをランダムに設定
        Tower tower = newObject.GetComponent<Tower>();
        tower.MaxCapacity = UnityEngine.Random.Range(MinTowerCapacity, MaxTowerCapacity);
        tower.NowAccCount = 0;
        tower.uuid = Guid.NewGuid().ToString();
        Towers.Add(newObject);
    }

    private void SpawnEvacuee() {
        // 親のGameObjectの子としてPrefabを生成
        Vector3 spawnPos = EvacueesSpawn.transform.position;
        GameObject newObject = Instantiate(Evacuee, spawnPos, Quaternion.identity);
        newObject.transform.parent = transform;
        newObject.tag = Tags.Evacuee;
        Evacuees.Add(newObject);
    }


    private void RemoveAllTowers() {
        GameObject[] towers = GameObject.FindGameObjectsWithTag(Tags.Tower);
        foreach (GameObject tower in towers) {
            Destroy(tower);
        }
        Towers.Clear();
    }

    private void RemoveAllEvacuees() {
        foreach (GameObject evacuee in Evacuees) {
            Destroy(evacuee);
        }
        Evacuees.Clear();
    }

    private Vector3 GenerateRandomPosition(Vector3 center, Vector3 size) {
        float x = UnityEngine.Random.Range(center.x - size.x / 2, center.x + size.x / 2);
        float z = UnityEngine.Random.Range(center.z - size.z / 2, center.z + size.z / 2);

        // 生成されたXとZ座標を使用して新しい位置Vector3を返す
        // y座標はSubFieldPlaneのy座標に合わせるか、必要に応じて調整
        return new Vector3(x, center.y, z);
    }

    /// <summary>
    /// Evacueesの全員が避難したかどうかを判定
    /// </summary>
    private bool isEvacueeAll() {
        foreach (GameObject evacuee in Evacuees) {
            Evacuee eva = evacuee.GetComponent<Evacuee>();
            if (!eva.isEvacuate) {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 全ての避難者が避難したときのイベントハンドラー
    /// </summary>
    private void OnEvacueeAllHandler() {
        Debug.Log("All Evacuees are evacuated");
        //全体報酬を避難率に応じて設定
        EvacuationRate = CalcEvacuationRate();
        // かかったステップ数を引き、制限時間に達した場合は報酬が０になるように設定
        float timeRate = m_ResetTimer / (float)MaxEnvironmentSteps;
        Agents.AddGroupReward(EvacuationRate - timeRate);
        Agents.EndGroupEpisode();
        init();
    }


    private void UpdateUI() {
        if (stepCounter != null) {
            stepCounter.text = $"Remain Steps : {MaxEnvironmentSteps - m_ResetTimer}";
        }
        if (evacRateCounter != null) {
            int currentRate = (int)(EvacuationRate * 100);
            evacRateCounter.text = $"Rate : {currentRate}%";
        }
    }

    private float CalcEvacuationRate() {
        int evacuatedCount = 0;
        foreach (GameObject evacuee in Evacuees) {
            if (!evacuee.activeSelf) {
                evacuatedCount++;
            }
        }
        return (float)evacuatedCount / Evacuees.Count;
    }


}
