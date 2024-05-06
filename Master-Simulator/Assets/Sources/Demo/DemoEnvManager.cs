using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// デモ用環境マネージャ.Fieldオブジェクトにアタッチ
/// </summary>
public class DemoEnvManager : MonoBehaviour {
    public GameObject TargetBalloon;
    public GameObject DroneAgent;

    void Start() {

    }

    public void InitRandomPosInField(GameObject obj) {
        Renderer parentRender = GetComponent<Renderer>();
        Bounds parentBounds = parentRender.bounds;

        float x = Random.Range(parentBounds.min.x, parentBounds.max.x);
        float y = Random.Range(parentBounds.min.y, parentBounds.max.y);
        float z = Random.Range(parentBounds.min.z, parentBounds.max.z);
        Vector3 pos = new Vector3(x, y, z);

        obj.transform.position = pos;
    }
}
