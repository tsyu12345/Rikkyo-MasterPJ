using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 記録用データ作成を司るクラス
/// </summary>
public class DataSaver {
    

    /// <summary>
    /// 任意のデータをCSV形式で保存します。
    /// </summary>
    /// <param name="filePath">保存先のファイルパス</param>
    /// <param name="header">CSVのヘッダー行</param>
    /// <param name="data">保存するデータのリスト（各行は文字列配列として渡す）</param>
    public static void SaveData(string filePath, string[] header, List<string[]> data) {
        // フォルダが存在しない場合は作成
        if (!Directory.Exists(filePath)) {
            Directory.CreateDirectory(filePath);
        }

        using (StreamWriter writer = new StreamWriter(filePath)) {
            // ヘッダーを記録
            if (header != null && header.Length > 0) {
                writer.WriteLine(string.Join(",", header));
            }

            // データを記録
            foreach (var row in data) {
                writer.WriteLine(string.Join(",", row));
            }

            Debug.Log($"Data saved to {filePath}");
        }
    }

}