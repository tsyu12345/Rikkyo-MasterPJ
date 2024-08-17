using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Constants;

/// <summary>
/// 津波避難タワーに関するスクリプト（オブジェクト１台分）
/// 現在の収容人数や、受け入れ可否等のデータを用意
/// </summary>
public class Tower : MonoBehaviour{
    public int MaxCapacity; //最大収容人数
    public int NowAccCount; //現在の収容人数
    public int currentCapacity; //現在の受け入れ可能人数：最大収容人数 - 現在の収容人数

    public string uuid; //タワーの識別子

    private string LogPrefix = "Tower: ";

    /**Events */
    public delegate void AcceptRejected(int NowAccCount) ; //収容定員が超過した時に発火する
    public AcceptRejected onRejected;

    private MeshRenderer ExMark; //受け入れ不可を示すマーク

    private EnvManager _env;

    void Start() {
        ExMark = transform.Find("ExMark").GetComponent<MeshRenderer>();
        ExMark.enabled = false;
        _env = GetComponentInParent<EnvManager>();
        _env.OnEndEpisode += (float _) => {
            NowAccCount = 0;
            ExMark.enabled = false;
        };
    }

    void Update() {
        currentCapacity = MaxCapacity - NowAccCount;
        if (currentCapacity <= 0) {
            onRejected?.Invoke(NowAccCount);
            ExMark.enabled = true;
        }
    }

    void OnTriggerEnter(Collider other) {
        if (other.CompareTag(Tags.Evacuee)) {
            Evacuee evacuee = other.GetComponent<Evacuee>();
            evacuee.Evacuation(this.gameObject);
        }
    }
}
