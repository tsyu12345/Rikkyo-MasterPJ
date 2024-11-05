using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using UnityEditor.SceneManagement;

public class AddNavMeshObstacleEditor : MonoBehaviour
{
    [MenuItem("Tools/Add NavMeshObstacle to Selected Objects")]
    private static void AddNavMeshObstacle()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            if (obj.GetComponent<NavMeshObstacle>() == null)
            {
                obj.AddComponent<NavMeshObstacle>();
                EditorUtility.SetDirty(obj); // 変更をマークしてシーンに保存可能にする
                Debug.Log($"{obj.name} に NavMeshObstacle を追加しました。");
            }
            else
            {
                Debug.Log($"{obj.name} には既に NavMeshObstacle が存在します。");
            }
        }
        
        // シーンの保存を強制
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
