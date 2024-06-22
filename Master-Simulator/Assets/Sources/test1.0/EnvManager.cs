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
    public GameObject TowerSpawn;
    public GameObject EvacueesSpawn;
    public List<GameObject> Drones;
    public List<GameObject> Evacuees;
    public List<GameObject> Towers;
    public GameObject floor;
    
    [Header("UI Elements")]
    public TextMeshProUGUI stepCounter;
    public TextMeshProUGUI evacRateCounter;
    public TextMeshProUGUI remainAgentsCounter;

    public int AgentGuidedCount = 0;

    public Utils Util;

    public delegate void EvacueeAllHandler();
    public EvacueeAllHandler OnEvacueeAll;
    public delegate void EndEpisodeHandler(float evacueeRate);
    public EndEpisodeHandler OnEndEpisode;

    private SimpleMultiAgentGroup Agents;
    private string LogPrefix = "EnvManager: ";
    private int m_ResetTimer;
    private delegate void SpawnCallback(GameObject obj);

    void Start() {
        Agents = new SimpleMultiAgentGroup();
        Util = GetComponent<Utils>();
        init();

        OnEvacueeAll += () => {
            AddGroupReward();
            init();
            Agents.EndGroupEpisode();
        };
        OnEndEpisode += (float evacueeRate) => {
            AddGroupReward();
            init();
            Agents.GroupEpisodeInterrupted();
        };
    }

    void FixedUpdate() {
        m_ResetTimer += 1;
        if (isEvacueeAll()) {
            OnEvacueeAll?.Invoke();
        }
        //残存するステップ数が制限時間に達した場合 or エージェントが全滅した場合、エピソードを終了
        var remainAgents = Agents.GetRegisteredAgents();
        if ((m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0) || remainAgents.Count < 1) {
            OnEndEpisode?.Invoke(EvacuationRate);
        }
        EvacuationRate = CalcEvacuationRate();
        UpdateUI();
    }

    /// <summary>
    /// 環境の初期化,全体エピソード開始時にコールされる
    /// </summary>
    public void init() {
        RemoveObjectAll(Tags.Tower);
        RemoveObjectAll(Tags.Evacuee);
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

        int countEvacuees = UnityEngine.Random.Range(MinEvacueeCount, MaxEvacueeCount);
        for(int i = 0; i < countEvacuees; i++) {
            SpawnObjects(Evacuee, EvacueesSpawn, (evacueeObj)=> {
                evacueeObj.tag = Tags.Evacuee;
                Evacuees.Add(evacueeObj);
            });
        }
    }

    public void UnregisterAgent(GameObject drone) {
        Agent agent = drone.GetComponent<Agent>();
        Agents.UnregisterAgent(agent);
    }


    private void RegisterAgents(string agentTag) {
        GameObject[] agents = GameObject.FindGameObjectsWithTag(Tags.Agent);
        
        foreach (GameObject agent in agents) {
            Agents.RegisterAgent(agent.GetComponent<Agent>());
        }
    }


    private void SpawnObjects(GameObject obj, GameObject spawnArea = null, SpawnCallback callback = null) {
        if (spawnArea == null) {
            spawnArea = floor;
        }
        Vector3 size = spawnArea.GetComponent<Collider>().bounds.size;
        Vector3 center = spawnArea.transform.position;
        Vector3 randomPosition = GenerateRandomPosition(center, size);
        GameObject newObject = Instantiate(obj, randomPosition, Quaternion.identity);
        newObject.transform.parent = transform;
        callback?.Invoke(newObject);
    }

    private void RemoveObjectAll(string tag) {
        GameObject[] objects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in objects) {
            if (obj.CompareTag(tag)) {
                Destroy(obj);
            }
        }
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

    private void UpdateUI() {
        if (stepCounter != null) {
            stepCounter.text = $"Remain Steps : {MaxEnvironmentSteps - m_ResetTimer}";
        }
        if (evacRateCounter != null) {
            int currentRate = (int)(EvacuationRate * 100);
            evacRateCounter.text = $"Rate : {currentRate}%";
        }
        if (remainAgentsCounter != null) {
            remainAgentsCounter.text = $"Remain Agents : {Agents.GetRegisteredAgents().Count}";
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


    private void AddGroupReward() {
        EvacuationRate = CalcEvacuationRate();
        // かかったステップ数を引き、制限時間に達した場合は報酬が０になるように設定
        float timeRate = m_ResetTimer / (float)MaxEnvironmentSteps;
        Agents.SetGroupReward(EvacuationRate);
        Agents.AddGroupReward(-timeRate);
    }


}
