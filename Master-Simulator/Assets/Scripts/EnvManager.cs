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
    public enum SimulateModeSetting {
        EvacueesOnly,
        AgentsModel
    }
    public enum EvacueeSpawnModeSetting {
        SingleRandom,
        Group
    }
    public enum TrainerMode {
        Train,
        Inference
    }
    public enum EvacueeSpeedSetting {
        Random,
        Constant
    }
    public enum LimitTimeModeSetting {
        Random,
        Constant,
        Stage
    }
    [Header("シミュレーションモード選択")]
    public SimulateModeSetting SimulateMode = SimulateModeSetting.EvacueesOnly;
    public EvacueeSpawnModeSetting EvacueeSpawnPosMode = EvacueeSpawnModeSetting.SingleRandom;
    public TrainerMode TrainMode = TrainerMode.Train;
    public EvacueeSpeedSetting EvacueeSpeedMode = EvacueeSpeedSetting.Random;
    public LimitTimeModeSetting LimitTimeMode = LimitTimeModeSetting.Random;
    [Header("避難者スポーン設定")]
    public int EvacueeSpawnSizePerDroneMin = 10;
    public int EvacueeSpawnSizePerDroneMax = 20;
    [Header("避難者スピード設定")]
    public float EvacueeSpeedMin = 5.0f;
    public float EvacueeSpeedMax = 15.0f;
    public float ConstantEvacueeSpeed = 5.0f;
    [Header("制限時間設定")]
    public float MinLimitTimeSec = 60.0f;
    public float MaxLimitTimeSec = 300.0f;
    public float LimitTimeSec = 120.0f;
    [Header("その他設定")]
    public int TimeScale = 20;


    [Header("Environment Parameters")]
    public float EvacuationRate = 0.0f;

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
    protected int m_ResetTimer;
    protected float totalElpTimeSec = 0.0f;
    protected delegate void SpawnCallback(GameObject obj);
    private List<List<float>> evacueeRateDatas = new List<List<float>>();
    private int currentEpisodeCount = 0;
    private string dataSavePath = "Assets/Datas/";

    /** 抽象メソッド */
    public abstract void InitEnv();

    public virtual void Start() {
        Time.timeScale = TimeScale;
        var uuid = Guid.NewGuid().ToString();
        var type = SimulateMode == SimulateModeSetting.EvacueesOnly ? "EvacueesOnly" : "AgentsModel";
        dataSavePath += $"{SceneManager.GetActiveScene().name}_{type}_{uuid}/";
        
        NavMesh.pathfindingIterationsPerFrame = 10000; //#47 パス検索の最大イテレーション数を設定
    
        Agents = new SimpleMultiAgentGroup();
        
        Util = GetComponent<Utils>();
        Init();
        SetEpisodeEndHandlers();
    }

    void FixedUpdate() {
        UpdateSimulate();
        UpdateUI();
    }

    /// <summary>
    /// 環境の初期化,全体エピソード開始時にコールされる
    /// </summary>
    public void Init() {
        
        if(LimitTimeMode == LimitTimeModeSetting.Random) { // #66 制限時間のランダム化
            LimitTimeSec = UnityEngine.Random.Range(MinLimitTimeSec, MaxLimitTimeSec);
        } else if(LimitTimeMode == LimitTimeModeSetting.Stage) { 
            // #TODO : #69 段階的に制限時間を設定する機能
        } else { // 固定値の制限時間
            LimitTimeSec = LimitTimeSec;
        }
        totalElpTimeSec = 0;
        m_ResetTimer = 0;
        InitEnv(); //継承先の子環境の初期化メソッド
        OnEpisodeInitialize?.Invoke(); // 環境準備完了のイベント発行
    }

    public void UnregisterAgent(GameObject drone) {
        Agent agent = drone.GetComponent<Agent>();
        if(agent != null) Agents.UnregisterAgent(agent);
    }

    private void UpdateSimulate() {
        m_ResetTimer += 1;
        totalElpTimeSec += Time.deltaTime;
        EvacuationRate = CalcEvacuationRate();

        evacueeRateDatas.Add(new List<float> {EvacuationRate, totalElpTimeSec});        

        bool allEvacuees = isEvacueeAll();
        //bool shouldEndEpisode = m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0;
        bool shouldEndEpisode = totalElpTimeSec >= LimitTimeSec && LimitTimeSec > 0;
        if (allEvacuees) {
            OnEvacueeAll?.Invoke(EvacuationRate);
        } else if (SimulateMode == SimulateModeSetting.AgentsModel) {
            var remainAgents = Agents.GetRegisteredAgents();
            if (remainAgents.Count < 1 || shouldEndEpisode) {
                OnEndEpisode?.Invoke(EvacuationRate);
            }
        } else if (shouldEndEpisode) {
            OnEndEpisode?.Invoke(EvacuationRate);
        }
    }


    private void HandleEndEpisode(float evacueeRate, bool isEvacueeAll) {
        var type = SimulateMode == SimulateModeSetting.EvacueesOnly ? "EvacueesOnly" : "AgentsModel";
        var fileName = $"{SceneManager.GetActiveScene().name}_{type}_Ep-{currentEpisodeCount}_evacuationRate.csv";
        if(TrainMode == TrainerMode.Inference) {
            SaveDatas(dataSavePath + fileName);
        }
        if(SimulateMode == SimulateModeSetting.AgentsModel) {
            // エージェントのエピソード終了処理を発行
            foreach(GameObject drone in Drones) {
                var agent = drone.GetComponent<DroneNavAgent>();
                agent.OnEndEpisodeHandler(evacueeRate);
            }
            AddGroupReward();
            if(isEvacueeAll) {
                Agents.EndGroupEpisode();
            } else {
                Agents.GroupEpisodeInterrupted();
            }
        }
        currentEpisodeCount++;
        evacueeRateDatas.Clear();
        Init();
    }

    private void SetEpisodeEndHandlers() {
        OnEvacueeAll += (evacueeRate) => {
            HandleEndEpisode(evacueeRate, true);
        };
        OnEndEpisode += (evacueeRate) => {
            HandleEndEpisode(evacueeRate, false);
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
            stepCounter.text = $"Remain Seconds : {LimitTimeSec - totalElpTimeSec}";
        }
        if (evacRateCounter != null) {
            int currentRate = (int)(EvacuationRate * 100);
            evacRateCounter.text = $"Rate : {currentRate}%";
        }
        if (remainAgentsCounter != null && SimulateMode == SimulateModeSetting.AgentsModel) {
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

    /// <summary>
    /// グループ報酬の報酬関数
    /// </summary>
    private void AddGroupReward() {
        Agents.SetGroupReward(EvacuationRate * 100);
    }

    private void SaveDatas(string filePath) {
        //フォルダが存在しない場合は作成
        if (!Directory.Exists(dataSavePath)) {
            Directory.CreateDirectory(dataSavePath);
        }
        using (StreamWriter writer = new StreamWriter(filePath)) {
            // 以下に記録したいデータを記述
            writer.WriteLine("Evacuation Rate, Elapsed Sec");
            for(int i = 0; i < evacueeRateDatas.Count; i++) { 
                writer.WriteLine($"{evacueeRateDatas[i][0]}, {evacueeRateDatas[i][1]}");
            }
            writer.Close();
            Debug.Log($"Data saved to {filePath}");
        }
    }

}
