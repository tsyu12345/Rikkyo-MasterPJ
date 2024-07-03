using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.AI;
using Constants;

/// <summary>
/// 物理演算でドローンを制御するクラス
/// </summary>
public class PhysicsController : DroneController {

    /// <summary>
    /// （物理コントロール専用）ドローンの移動制御関数
    /// </summary>
   /// <param name="actions"></param>
    public override void FlyingCtrl(ActionBuffers actions) {
        float horInput = actions.ContinuousActions[(int)DroneCtrlIndex.Horizontal]; //水平方向の入力(左右)
        float verInput = actions.ContinuousActions[(int)DroneCtrlIndex.Vertical]; //垂直方向の入力（前後）
        float rotInput = actions.ContinuousActions[(int)DroneCtrlIndex.Rotation]; //回転方向の入力
        float speedInput = actions.ContinuousActions[(int)DroneCtrlIndex.Speed]; //速度の入力
        //float altInput = actions.ContinuousActions[(int)DroneCtrlIndex.Altitude]; //高度方向の入力(上下)
        moveSpeed = speedInput * 10f; //速度の入力値を10倍して移動速度に設定
        
        if (batteryLevel <= 0) {
            return;
        }
        
        if(horInput > 0) {
            Right(horInput);
        } else {
            Left(Mathf.Abs(horInput));
        }
        if(verInput > 0) {
            Forward(verInput);
        } else {
            Back(Mathf.Abs(verInput));
        }
        //回転
        if(rotInput > 0) {
            Cw(rotInput);
        } else if (rotInput < 0) {
            Ccw(Mathf.Abs(rotInput));
        }
        //高度方向の移動
        /*
        if(altInput > 0) {
            Up(altInput);
        } else if (altInput < 0) {
            Down(Mathf.Abs(altInput));
        }
        */
    }

    public override void InHeuristicCtrl(in ActionBuffers actionsOut) {
        var action = actionsOut.ContinuousActions;
        
        if(Input.GetKey(KeyCode.W)) {
            //前進
            action[(int)DroneCtrlIndex.Vertical] = 1f;
        } else if (Input.GetKey(KeyCode.S)) {
            //後退
            action[(int)DroneCtrlIndex.Vertical] = -1f;
        }

        if (Input.GetKey(KeyCode.A)) {
            //左移動
            action[(int)DroneCtrlIndex.Horizontal] = -1f;
        } else if (Input.GetKey(KeyCode.D)) {
            //右移動
            action[(int)DroneCtrlIndex.Horizontal] = 1f;
        } 

        if (Input.GetKey(KeyCode.LeftArrow)) {
            //左回転
            action[(int)DroneCtrlIndex.Rotation] = -1f;
        } else if (Input.GetKey(KeyCode.RightArrow)) {
            //右回転
            action[(int)DroneCtrlIndex.Rotation] = 1f;
        }
        
        if (Input.GetKey(KeyCode.Space)) {
            //上昇
            action[(int)DroneCtrlIndex.Altitude] = 1f;
        } else if (Input.GetKey(KeyCode.LeftShift)) {
            //下降
            action[(int)DroneCtrlIndex.Altitude] = -1f;
        }

        //スロットルの調整
        if (Input.GetKey(KeyCode.UpArrow)) {
            action[(int)DroneCtrlIndex.Speed] += 1f;
        } else if (Input.GetKey(KeyCode.DownArrow)) {
            action[(int)DroneCtrlIndex.Speed] += -1f;
        }
    }


    //NOTE：以下はTello SDKを参考
    private static Vector3 RenewPosLerp(Vector3 currentPos, Vector3 targetPos, float speed) {
        return Vector3.Lerp(currentPos, targetPos, speed * Time.deltaTime);
    }


    /// <summary>
    /// 機体を上昇させる
    /// </summary>
    /// <param name="value">どのくらいの座標上昇させるか</param>
    private void Up(float value) {
        Vector3 force = Vector3.up * value * moveSpeed;
        Rbody.AddForce(force);
    }

    /// <summary>
    /// 機体を下降させる
    /// </summary>
    /// <param name="value">どのくらいの座標下降させるか</param>
    private void Down(float value) {
        Vector3 force = Vector3.down * value * moveSpeed;
        Rbody.AddForce(force);
    }

    /// <summary>
    /// 前方に移動させる
    /// </summary>
    /// <param name="value">どのくらいの座標前進させるか</param>
    private void Forward(float value)
    {
        Vector3 force = transform.forward * value * moveSpeed;
        Rbody.AddForce(force);
    }

    /// <summary>
    /// 後方に移動させる
    /// </summary>
    /// <param name="value">どのくらいの座標後退させるか</param>
    private void Back(float value) {
        Vector3 force = -transform.forward * value * moveSpeed;
        Rbody.AddForce(force);
    }

    /// <summary>
    /// 左に移動させる
    /// </summary>
    /// <param name="value">どのくらいの座標左移動させるか</param>
    private void Left(float value) {
        Vector3 force = -transform.right * value * moveSpeed;
        Rbody.AddForce(force);
    }

    /// <summary>
    /// 右に移動させる
    /// </summary>
    /// <param name="value">どのくらいの座標右移動させるか</param>
    private void Right(float value) {
        Vector3 force = transform.right * value * moveSpeed;
        Rbody.AddForce(force);
    }

    /// <summary>
    /// 時計回りに旋回
    /// </summary>
    /// <param name="value">時計回りにどのくらい旋回させるか</param>
    private void Cw(float value) {
        Vector3 torque = Vector3.up * value * moveSpeed * Time.deltaTime;
        Rbody.AddTorque(torque);
    }

    /// <summary>
    /// 反時計回りに機体を回転させる
    /// </summary>
    /// <param name="value">反時計回りにどのくらい旋回させるか</param>
    private void Ccw(float value) {
        Vector3 torque = Vector3.down * value * moveSpeed * Time.deltaTime;
        Rbody.AddTorque(torque);
    }
}