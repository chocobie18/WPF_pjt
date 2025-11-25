using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using AMPManager.Model;

namespace AMPManager.Core
{
    public class DatabaseManager
    {
        private const string ConnectionString = "Data Source=factory.db;Version=3;";

        public DatabaseManager()
        {
            EnsureTableStructure();
        }

        private void EnsureTableStructure()
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 이미지 저장을 위한 컬럼이 없으면 추가 (BLOB 타입)
                    var colsToAdd = new[] { "img_cam1", "img_cam2" };
                    foreach (var col in colsToAdd)
                    {
                        bool exists = false;
                        using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA table_info(MEASUREMENTS)", conn))
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) if (reader["name"].ToString() == col) exists = true;
                        }
                        if (!exists)
                        {
                            using (SQLiteCommand cmd = new SQLiteCommand($"ALTER TABLE MEASUREMENTS ADD COLUMN {col} BLOB", conn))
                                cmd.ExecuteNonQuery();
                        }
                    }

                    // result 컬럼 확인 (기존 코드 유지)
                    bool hasResult = false;
                    using (SQLiteCommand cmd = new SQLiteCommand($"PRAGMA table_info(MEASUREMENTS)", conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) if (reader["name"].ToString() == "result") hasResult = true;
                    }
                    if (!hasResult)
                    {
                        using (SQLiteCommand cmd = new SQLiteCommand("ALTER TABLE MEASUREMENTS ADD COLUMN result TEXT DEFAULT 'OK'", conn))
                            cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // [수정] 이미지 데이터(byte[])도 같이 저장
        public void InsertMeasurement(int productId, string time, bool isDefect, byte[]? img1, byte[]? img2)
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string resultStr = isDefect ? "NG" : "OK";
                    string query = "INSERT INTO MEASUREMENTS (productID, measurement_time, result, img_cam1, img_cam2) VALUES (@pid, @time, @res, @img1, @img2)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", productId);
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.Parameters.AddWithValue("@res", resultStr);
                        cmd.Parameters.AddWithValue("@img1", img1 ?? new byte[0]); // 없으면 빈 값
                        cmd.Parameters.AddWithValue("@img2", img2 ?? new byte[0]);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 저장 실패: {ex.Message}");
            }
        }

        public List<LogEntry> GetLogs()
        {
            var list = new List<LogEntry>();
            if (!File.Exists("factory.db")) return list;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // MID(고유번호)도 같이 가져와야 나중에 이미지를 찾을 수 있음
                    string query = @"
                        SELECT M.MID, M.measurement_time, P.name, M.result
                        FROM MEASUREMENTS M
                        LEFT JOIN PRODUCT P ON M.productID = P.PID
                        ORDER BY M.MID DESC LIMIT 100";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string res = reader["result"].ToString();
                            if (string.IsNullOrEmpty(res)) res = "OK";

                            list.Add(new LogEntry
                            {
                                Id = Convert.ToInt32(reader["MID"]), // [중요] ID 추가 필요 (Model 수정 예정)
                                Timestamp = reader["measurement_time"].ToString(),
                                PropertyName = reader["name"].ToString(),
                                Status = res == "NG" ? "불량" : "정상"
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        // [추가] 특정 로그의 이미지 가져오기
        public (byte[]?, byte[]?) GetLogImages(int mid)
        {
            if (!File.Exists("factory.db")) return (null, null);
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = "SELECT img_cam1, img_cam2 FROM MEASUREMENTS WHERE MID = @mid";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", mid);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                byte[]? i1 = reader["img_cam1"] as byte[];
                                byte[]? i2 = reader["img_cam2"] as byte[];
                                return (i1, i2);
                            }
                        }
                    }
                }
            }
            catch { }
            return (null, null);
        }

        // (통계 관련 함수들은 기존 코드 유지 - 생략)
        public Dictionary<string, double> GetDailyDefectRates(DateTime start, DateTime end) { return new Dictionary<string, double>(); /* 기존 코드 사용 */ }
        public (double w, double l, double c, double cp) GetAverageSpecs() { return (0, 0, 0, 0); /* 기존 코드 사용 */ }
    }
}