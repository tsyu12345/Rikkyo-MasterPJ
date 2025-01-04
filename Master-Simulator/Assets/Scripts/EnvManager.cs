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
    [Tooltip("避難者のみのシミュレーションかエージェントモデルを含むかの選択")]
    public SimulateModeSetting SimulateMode = SimulateModeSetting.EvacueesOnly;
    [Tooltip("避難者のスポーンモードの選択。SingleRandomはランダムな位置に一人ずつ、Groupは集団単位でスポーン")]
    public EvacueeSpawnModeSetting EvacueeSpawnPosMode = EvacueeSpawnModeSetting.SingleRandom;
    [Tooltip("トレーニングモードか推論モードかの選択。トレーニングモードの時はデータ記録を行いません")]
    public TrainerMode TrainMode = TrainerMode.Train;
    [Tooltip("避難者のスピード設定。Randomは個体毎にランダムなスピード、Constantは全個体一定のスピード")]
    public EvacueeSpeedSetting EvacueeSpeedMode = EvacueeSpeedSetting.Random;
    [Tooltip("制限時間の設定。Randomはランダムな制限時間、Constantは固定の制限時間、Stageは段階的に制限時間を更新")]
    public LimitTimeModeSetting LimitTimeMode = LimitTimeModeSetting.Random;
    [Header("避難者スポーン設定")]
    public int EvacueeSpawnSizePerDroneMin = 10;
    public int EvacueeSpawnSizePerDroneMax = 20;
    [Header("避難者スピード設定")]
    public float EvacueeSpeedMin = 5.0f;
    public float EvacueeSpeedMax = 15.0f;
    public float ConstantEvacueeSpeed = 5.0f;
    [Header("制限時間設定")]
    [Tooltip("制限時間がランダムな場合の下限")]
    public float MinLimitTimeSec = 60.0f;
    [Tooltip("制限時間がランダムな場合の上限")]
    public float MaxLimitTimeSec = 300.0f;
    [Tooltip("制限時間が定数な場合の制限時間")]
    public float LimitTimeSec = 120.0f;
    [Tooltip("制限時間を段階更新する場合の更新間隔")]
    public float LimitRenewIntervalSec = 100f;
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
    public float currentTimeSec;
    public string envUUID;
    protected float totalElpTimeSec = 0.0f;
    protected delegate void SpawnCallback(GameObject obj);
    private List<(float elapsedSec, float evacuationRate)> evacueeRateDatas = new List<(float elapsedSec, float evacuationRate)>();
    private int currentEpisodeCount = 0;
    private string dataSavePath = Path.Combine(Application.dataPath, "Data/");
    /** 抽象メソッド */
    public abstract void InitEnv();

    public virtual void Start() {
        Time.timeScale = TimeScale;
        envUUID = Guid.NewGuid().ToString();
        var type = SimulateMode == SimulateModeSetting.EvacueesOnly ? "EvacueesOnly" : "AgentsModel";
        dataSavePath = Path.Combine(dataSavePath, $"{SceneManager.GetActiveScene().name}_{type}_{envUUID}/");
        
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
            LimitTimeSec = MinLimitTimeSec + LimitRenewIntervalSec * currentEpisodeCount;
            if(LimitTimeSec > MaxLimitTimeSec) {
                LimitTimeSec = MaxLimitTimeSec;
            }
        } else { // 固定値の制限時間
            LimitTimeSec = LimitTimeSec;
        }
        totalElpTimeSec = 0;
        currentTimeSec = 0;
        InitEnv(); //継承先の子環境の初期化メソッド
        OnEpisodeInitialize?.Invoke(); // 環境準備完了のイベント発行
    }

    public void UnregisterAgent(GameObject drone) {
        Agent agent = drone.GetComponent<Agent>();
        if(agent != null) Agents.UnregisterAgent(agent);
    }

    private void UpdateSimulate() {
        currentTimeSec += 1;
        totalElpTimeSec += Time.deltaTime;
        EvacuationRate = CalcEvacuationRate();
        if(TrainMode == TrainerMode.Inference) {
            evacueeRateDatas.Add((totalElpTimeSec, EvacuationRate));        
        }

        bool allEvacuees = isEvacueeAll();
        //bool shouldEndEpisode = currentTimeSec >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0;
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

    /// <summary>
    /// エピソード終了時の処理
    /// </summary>
    private void HandleEndEpisode(float evacueeRate, bool isEvacueeAll) {
        /** データ集計処理群*/
        if(TrainMode == TrainerMode.Inference) {
            var type = SimulateMode == SimulateModeSetting.EvacueesOnly ? "EvacueesOnly" : "AgentsModel";
            /**環境全体の避難率推移の集計*/
            var folder = Path.Combine(dataSavePath, "EnvEvacuationRate/");
            var fileName = $"{SceneManager.GetActiveScene().name}_{type}_Ep-{currentEpisodeCount}_EnvEvacuationRate.csv";
            var path = folder + fileName;
            var header = new string[] {"Elapsed Sec", "Evacuation Rate"};
            DataSaver.SaveData2CSV(path, header, evacueeRateDatas, (data) => {
                return new string[] {data.elapsedSec.ToString(), data.evacuationRate.ToString()};
            });

            /** 各避難所毎の収容人数の推移の集計 */
            folder = Path.Combine(dataSavePath, "TowerEvacueeCount/");
            fileName = $"{SceneManager.GetActiveScene().name}_{type}_Ep-{currentEpisodeCount}_TowerEvacueeCount.csv";
            path = folder + fileName;
            header = new string[] {"Elapsed Sec", "Evacuee Count"};
            foreach(GameObject shelterObj in Towers) {
                Tower shelter = shelterObj.GetComponent<Tower>();
                DataSaver.SaveData2CSV(path, header, shelter.ElapsedAccData, (data) => {
                    return new string[] {data.elapsedSec.ToString(), data.accCount.ToString()};
                });
            }
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

}
