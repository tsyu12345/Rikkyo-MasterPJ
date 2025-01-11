using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[ExecuteAlways] // エディタ上で動作
public class NavMeshGridVisualizer : MonoBehaviour
{
    public Vector3 origin = Vector3.zero; // グリッドの開始位置
    public Vector3 size = new Vector3(10, 0, 10); // グリッドの範囲
    public float gridSpacing = 1.0f; // グリッド間隔
    public Color gridColor = Color.green; // グリッド表示色

    private List<Vector3> gridPoints = new List<Vector3>();

    void OnDrawGizmos()
    {
        Gizmos.color = gridColor;
        if (gridPoints == null || gridPoints.Count == 0)
        {
            GenerateGridPoints(); // グリッドを生成
        }

        foreach (var point in gridPoints)
        {
            Gizmos.DrawSphere(point, 0.1f); // 各グリッドポイントを球で表示
        }
    }

    private void GenerateGridPoints()
    {
        gridPoints.Clear();
        for (float x = origin.x; x < origin.x + size.x; x += gridSpacing)
        {
            for (float z = origin.z; z < origin.z + size.z; z += gridSpacing)
            {
                Vector3 point = new Vector3(x, origin.y, z);
                if (NavMesh.SamplePosition(point, out NavMeshHit hit, gridSpacing / 2, NavMesh.AllAreas))
                {
                    gridPoints.Add(hit.position); // NavMesh上の有効なポイントを追加
                }
            }
        }
    }

    // グリッドを手動で再生成するメソッド（インスペクターから呼び出し可能）
    [ContextMenu("Regenerate Grid")]
    public void RegenerateGrid()
    {
        GenerateGridPoints();
    }
}
