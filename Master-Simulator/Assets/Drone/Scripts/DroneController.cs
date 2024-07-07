using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;
using Constants;
/// <summary>
/// ドローン共通コンポーネント.
/// 全ドローンクラスの基底クラス.ドローンの操作、動作に関する処理
/// </summary>
public abstract class DroneController : MonoBehaviour {
    [Header("Movement Parameters")]
    public float moveSpeed = 10f; // 移動速度
    public float rotSpeed = 100f; // 回転速度

    [Header("Battery")]
    public float batteryLevel = 100f; // バッテリー残量の初期値
    private float batteryDrainRate = 1f; // 1秒あたりのバッテリー消費率

    [Header("Communication Parameters")]
    public float communicationRange = 10f; // 通信範囲(半径)
    public GameObject communicateArea; //通信電波域を表すオブジェクト円形(Droneの子要素として定義)
    [Header("Crash Objects")]
    public List<string> CrashTags; //衝突判定対象のタグ
    public Rigidbody Rbody;

    /**Events*/
    public delegate void OnReceiveMessage(Types.MessageData Data);
    public OnReceiveMessage onReceiveMsg;
    public delegate void OnCrash(Vector3 crashPos);
    public OnCrash onCrash;
    public delegate void OnEmptyButtery();
    public OnEmptyButtery onEmptyBattery;
    public delegate void OnChargingBattery();
    public OnChargingBattery onChargingBattery;
    /***/
    public List<string> CommunicateTargetTags;
    private string Team;

    public abstract void InHeuristicCtrl(in ActionBuffers actionsOut);
    public abstract void FlyingCtrl(ActionBuffers actions);


    protected void Start() {
        Rbody = GetComponent<Rigidbody>();
        //communicateArea.transform.localScale = new Vector3(communicationRange, communicationRange, communicationRange);
        StartCoroutine(BatteryDrainCoroutine());
    }

    void OnTriggerEnter(Collider other) {
        if (CrashTags.Contains(other.tag)) {
            FreeFall();
            onCrash?.Invoke(other.transform.position);
        }
    }

    void OnTriggerStay(Collider other) {
        if(other.tag == "Station") {
            onChargingBattery?.Invoke();
            Charge();
        }
    }

    public void RegisterTeam(string team) {
        Team = team;
    }
    public void AddCommunicateTarget(string target) {
        CommunicateTargetTags.Add(target);
    }

    /// <summary>
    /// 他のドローンにメッセージを送信する。
    /// </summary>
    public bool Communicate(Types.MessageData messageData, GameObject target) {
        var result = false;
        //一旦距離制限は考えない
        target.GetComponent<DroneController>().ReceiveMessage(messageData);
        result = true;
        return result;
    }

    /// <summary>
    /// 他のドローンからメッセージを受信する（させる）。
    /// </summary>
    public void ReceiveMessage(Types.MessageData Data) {
        onReceiveMsg?.Invoke(Data);
    }


    protected void FreeFall() {
        Rbody.useGravity = true;
        //FreezePosition,FreezeRotationを解除
        Rbody.constraints = RigidbodyConstraints.None;
    }

    protected void Charge() {
        //TODO:1秒ごとにバッテリーを充電
        batteryLevel = 100;
    }

    private IEnumerator BatteryDrainCoroutine() {
        while (batteryLevel > 0) {
            yield return new WaitForSeconds(1);
            batteryLevel -= batteryDrainRate;
            //Debug.Log($"Battery Level: {batteryLevel}%");
        }
        onEmptyBattery?.Invoke();
        FreeFall(); //TODO:イベントハンドラーに記載する
    }
}
