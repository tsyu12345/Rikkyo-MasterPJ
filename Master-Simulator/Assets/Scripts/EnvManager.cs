using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using UnityEngine.AI;
using UnityEngine;
using Constants;
using UtilityFuncs;
using UnityEngine.UI;
using TMPro; 
using UnityEngine.SceneManagement;

/// <summary>
/// 環境に関するスクリプトの管理
/// その基底クラス 
/// </summary>
public abstract class EnvManager : MonoBehaviour {
    [Header("SImulator Settings")]
    [Tooltip("エージェントを省いた単純な避難者のみのシミュレーションを行います")]
    public bool OnlyEvacuees = false;
    [Tooltip("モデルトレーニングを行うかどうか")]
    public bool modelTrain = false;
    [Tooltip("避難者の速度をランダムに設定します。")]
    public bool EnableRandmizeSpeedEvacuee = false;
    public float EvacueeSpeedMin = 5.0f;
    public float EvacueeSpeedMax = 15.0f;
    public float ConstantEvacueeSpeed = 5.0f;


    [Header("Environment Parameters")]
    public float EvacuationRate = 0.0f;
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 1000; 

    [Header("GameObjects")]
    public GameObject Evacuee;

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

    public delegate void EvacueeAllHandler(float evacueeRate);
    public EvacueeAllHandler OnEvacueeAll;
    public delegate void EndEpisodeHandler(float evacueeRate);
    public EndEpisodeHandler OnEndEpisode;
    public delegate void EpisodeInitializeHandler();
    public EpisodeInitializeHandler OnEpisodeInitialize;

    private SimpleMultiAgentGroup Agents;
    protected string LogPrefix = "EnvManager: ";
    protected int m_ResetTimer;
    protected delegate void SpawnCallback(GameObject obj);
    private List<float> evacueeRateDatas = new List<float>();
    private int currentEpisodeCount = 0;
    private string dataSavePath = "Assets/Datas/";

    /** 抽象メソッド */
    public abstract void InitEnv();

    public virtual void Start() {
        //Drones = new List<GameObject>();
        NavMesh.pathfindingIterationsPerFrame = 10000; //#47 パス検索の最大イテレーション数を設定
        if(!OnlyEvacuees) {
            Agents = new SimpleMultiAgentGroup();
        }
        Util = GetComponent<Utils>();
        Init();
        SetEpisodeEndHandlers();
    }

    void FixedUpdate() {
        m_ResetTimer += 1;
        EvacuationRate = CalcEvacuationRate();
        evacueeRateDatas.Add(EvacuationRate); // 1step毎に計測

        bool allEvacuees = isEvacueeAll();
        bool shouldEndEpisode = m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0;
        if (allEvacuees) {
            OnEvacueeAll?.Invoke(EvacuationRate);
        } else if (!OnlyEvacuees) {
            var remainAgents = Agents.GetRegisteredAgents();
            if (remainAgents.Count < 1 || shouldEndEpisode) {
                OnEndEpisode?.Invoke(EvacuationRate);
            }
        } else if (shouldEndEpisode) {
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
        //drone.SetActive(false);
    }

    private void SetEpisodeEndHandlers() {
        OnEvacueeAll += (float evacueeRate) => {
            var type = OnlyEvacuees ? "EvacueesOnly" : "AgentsModel";
            var fileName = $"{SceneManager.GetActiveScene().name}_{type}_Ep-{currentEpisodeCount}_evacuationRate.csv";
            if(!modelTrain) {
                SaveDatas(dataSavePath + fileName);
            }
            if(!OnlyEvacuees) {
                // エージェントのエピソード終了処理を発行
                foreach(GameObject drone in Drones) {
                    var agent = drone.GetComponent<DroneNavAgent>();
                    agent.OnEndEpisodeHandler(evacueeRate);
                }
                AddGroupReward();
                Agents.EndGroupEpisode();
            }
            currentEpisodeCount++;
            evacueeRateDatas.Clear();
            Init();
        };
        OnEndEpisode += (float evacueeRate) => {
            var type = OnlyEvacuees ? "EvacueesOnly" : "AgentsModel";
            var fileName = $"{SceneManager.GetActiveScene().name}_{type}_Ep-{currentEpisodeCount}_evacuationRate.csv";
            if(!modelTrain) {
                SaveDatas(dataSavePath + fileName);
            }
            if(!OnlyEvacuees) {
                // エージェントのエピソード終了処理を発行
                foreach(GameObject drone in Drones) {
                    var agent = drone.GetComponent<DroneNavAgent>();
                    agent.OnEndEpisodeHandler(evacueeRate);
                }
                AddGroupReward();
                Agents.GroupEpisodeInterrupted();
            }
            currentEpisodeCount++;
            evacueeRateDatas.Clear();
            Init();
        };
    }

    protected void RegisterAgents(string agentTag) {
        GameObject[] agents = GameObject.FindGameObjectsWithTag(Tags.Agent);
        
        foreach (GameObject agent in agents) {
            Agents.RegisterAgent(agent.GetComponent<Agent>());
            agent.SetActive(true);
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
        if (remainAgentsCounter != null && !OnlyEvacuees) {
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
        Agents.AddGroupReward(EvacuationRate);
    }

    private void SaveDatas(string filePath) {
        using (StreamWriter writer = new StreamWriter(filePath)) {
            // 以下に記録したいデータを記述
            writer.WriteLine("Evacuation Rate, Time");
            for(int i = 0; i < evacueeRateDatas.Count; i++) { 
                writer.WriteLine($"{evacueeRateDatas[i]}, {i}");
            }
            writer.Close();
            Debug.Log($"Data saved to {filePath}");
        }
    }

}
