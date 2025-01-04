using System;
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
    /// <typeparam name="T">保存するデータの型</typeparam>
    /// <param name="filePath">保存先のファイルパス</param>
    /// <param name="header">CSVのヘッダー行</param>
    /// <param name="data">保存するデータのリスト</param>
    /// <param name="convertToRow">データ型 T を文字列配列に変換する関数</param>
    public static void SaveData2CSV<T>(string filePath, string[] header, List<T> data, Func<T, string[]> convertToRow) {
        // 保存先フォルダのディレクトリパスを取得
        string directoryPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryPath)) {
            Directory.CreateDirectory(directoryPath);
        }

        using (StreamWriter writer = new StreamWriter(filePath)) {
            // ヘッダーを記録
            if (header != null && header.Length > 0) {
                writer.WriteLine(string.Join(",", header));
            }

            // データを記録
            foreach (var item in data) {
                string[] row = convertToRow(item); // データを文字列配列に変換
                writer.WriteLine(string.Join(",", row));
            }

            Debug.Log($"Data saved to {filePath}");
        }
    }
}

