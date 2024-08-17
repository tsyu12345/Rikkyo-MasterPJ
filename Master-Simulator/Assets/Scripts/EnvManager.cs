using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine.AI;
using UnityEngine;
using Constants;
using UtilityFuncs;
using UnityEngine.UI;
using TMPro; 

/// <summary>
/// 環境に関するスクリプトの管理
/// その基底クラス 
/// </summary>
public abstract class EnvManager : MonoBehaviour {

    [Header("Environment Parameters")]
    public float EvacuationRate = 0.0f;
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 1000; 
    /**避難者の設定*/
    public int MinEvacueeCount = 1;
    public int MaxEvacueeCount = 10;

    [Header("GameObjects")]
    public GameObject Evacuee;
    public abstract List<GameObject> EvacueesSpawnAreas { get; set; }

    [Header("Objects")]
    public List<GameObject> Drones;
    public List<GameObject> Evacuees;
    public bool allEvacueesReady = false; // 全ての避難者がパス検索を終えたかどうか
    public List<GameObject> Towers;
    
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
    public delegate void EpisodeInitializeHandler();
    public EpisodeInitializeHandler OnEpisodeInitialize;

    private SimpleMultiAgentGroup Agents;
    protected string LogPrefix = "EnvManager: ";
    protected int m_ResetTimer;
    protected delegate void SpawnCallback(GameObject obj);

    /** 抽象メソッド */
    public abstract void InitEnv();

    public virtual void Start() {
        NavMesh.pathfindingIterationsPerFrame = 10000; //#47 パス検索の最大イテレーション数を設定
        Agents = new SimpleMultiAgentGroup();
        Util = GetComponent<Utils>();
        Init();

        OnEvacueeAll += () => {
            AddGroupReward();
            Agents.EndGroupEpisode();
            Init();
        };
        OnEndEpisode += (float evacueeRate) => {
            AddGroupReward();
            Agents.GroupEpisodeInterrupted();
            Init();
        };
    }

    void FixedUpdate() {
        m_ResetTimer += 1;
        EvacuationRate = CalcEvacuationRate();
        
        if (isEvacueeAll()) {
            OnEvacueeAll?.Invoke();
        }
        //残存するステップ数が制限時間に達した場合 or エージェントが全滅した場合、エピソードを終了
        var remainAgents = Agents.GetRegisteredAgents();
        if ((m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0) || remainAgents.Count < 1) {
            OnEndEpisode?.Invoke(EvacuationRate);
        }
        UpdateUI();
    }

    /// <summary>
    /// 環境の初期化,全体エピソード開始時にコールされる
    /// </summary>
    public void Init() {
        InitEnv();
        OnEpisodeInitialize?.Invoke();
    }

    public void UnregisterAgent(GameObject drone) {
        Agent agent = drone.GetComponent<Agent>();
        Agents.UnregisterAgent(agent);
    }

    protected void RegisterAgents(string agentTag) {
        GameObject[] agents = GameObject.FindGameObjectsWithTag(Tags.Agent);
        
        foreach (GameObject agent in agents) {
            Agents.RegisterAgent(agent.GetComponent<Agent>());
        }
    }


    protected void SpawnObject(GameObject obj, GameObject spawnArea, SpawnCallback callback = null) {
        Vector3 size = spawnArea.GetComponent<Collider>().bounds.size;
        Vector3 center = spawnArea.transform.position;
        Vector3 randomPosition = GenerateRandomPosition(center, size);
        GameObject newObject = Instantiate(obj, randomPosition, Quaternion.identity);
        newObject.transform.parent = transform;
        callback?.Invoke(newObject);
    }

    protected void RemoveObjectAll(string tag) {
        GameObject[] objects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in objects) {
            if (obj.CompareTag(tag)) {
                Destroy(obj);
            }
        }
    }


    protected Vector3 GenerateRandomPosition(Vector3 center, Vector3 size) {
        float x = UnityEngine.Random.Range(center.x - size.x / 2, center.x + size.x / 2);
        float z = UnityEngine.Random.Range(center.z - size.z / 2, center.z + size.z / 2);

        // 生成されたXとZ座標を使用して新しい位置Vector3を返す
        // y座標はSubFieldPlaneのy座標に合わせるか、必要に応じて調整
        return new Vector3(x, center.y, z);
    }

    /// <summary>
    /// Evacueesの全員が避難したかどうかを判定
    /// </summary>
    protected bool isEvacueeAll() {
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
        Agents.SetGroupReward(AgentGuidedCount);
    }

}
