using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using AMPManager.Model;

namespace AMPManager.Core
{
    public class DatabaseManager
    {
        // DB 파일 경로
        private const string ConnectionString = "Data Source=factory.db;Version=3;";

        // [기능 1] 계측 결과 저장하기 (INSERT) -> 알고리즘이 완료될 때 이 함수를 호출하면 됨
        public void InsertMeasurement(int productId, string time)
        {
            if (!File.Exists("factory.db")) return;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO MEASUREMENTS (productID, measurement_time) VALUES (@pid, @time)";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@pid", productId);
                        cmd.Parameters.AddWithValue("@time", time);
                        cmd.ExecuteNonQuery(); // 실행!
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 저장 실패: {ex.Message}");
            }
        }

        // [기능 2] 로그 불러오기 (SELECT) -> LogViewModel에서 사용
        public List<LogEntry> GetLogs()
        {
            var list = new List<LogEntry>();
            if (!File.Exists("factory.db")) return list;

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 최신순으로 100개만 가져오기
                    string query = @"
                        SELECT M.measurement_time, P.name, '측정 완료' AS status
                        FROM MEASUREMENTS M
                        LEFT JOIN PRODUCT P ON M.productID = P.PID
                        ORDER BY M.MID DESC LIMIT 100";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new LogEntry
                            {
                                Timestamp = reader["measurement_time"].ToString(),
                                PropertyName = reader["name"].ToString(),
                                Status = reader["status"].ToString()
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }
    }
}