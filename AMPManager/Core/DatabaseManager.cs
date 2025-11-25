using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Diagnostics; // [추가] 로그 출력을 위해 필요
using AMPManager.Model;

namespace AMPManager.Core
{
    public class DatabaseManager
    {
        // DB 파일 경로 설정
        private const string DbFileName = "factory.db";
        private const string ConnectionString = $"Data Source={DbFileName};Version=3;";

        public DatabaseManager()
        {
            // [수정] 파일이 존재하지 않으면 DB 파일과 테이블을 새로 생성합니다.
            if (!File.Exists(DbFileName))
            {
                try
                {
                    SQLiteConnection.CreateFile(DbFileName);

                    using (var conn = new SQLiteConnection(ConnectionString))
                    {
                        conn.Open();

                        // 테이블 생성 쿼리 (측정 기록 테이블 + 제품 정보 테이블)
                        string sql = @"
                            CREATE TABLE IF NOT EXISTS MEASUREMENTS (
                                MID INTEGER PRIMARY KEY AUTOINCREMENT,
                                productID INTEGER,
                                measurement_time TEXT
                            );
                            CREATE TABLE IF NOT EXISTS PRODUCT (
                                PID INTEGER PRIMARY KEY AUTOINCREMENT,
                                name TEXT
                            );
                            -- 기본 제품 데이터 하나 추가 (없으면 JOIN 시 이름이 안 나옴)
                            INSERT INTO PRODUCT (name) VALUES ('M6 Bolt');
                        ";

                        using (var cmd = new SQLiteCommand(sql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                    Debug.WriteLine("DB 파일 및 초기 테이블 생성 완료.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"DB 초기화 중 오류 발생: {ex.Message}");
                }
            }
        }

        // [기능 1] 계측 결과 저장하기 (INSERT)
        public void InsertMeasurement(int productId, string time)
        {
            // [수정] 파일 체크 로직 삭제 (생성자에서 처리함)
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
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DB 저장 실패: {ex.Message}");
            }
        }

        // [기능 2] 로그 불러오기 (SELECT)
        public List<LogEntry> GetLogs()
        {
            var list = new List<LogEntry>();

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // [수정] JOIN을 사용하여 제품 이름(P.name)까지 가져오도록 쿼리 개선
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
                                // null일 경우를 대비해 안전하게 변환
                                Timestamp = reader["measurement_time"]?.ToString() ?? "",
                                PropertyName = reader["name"]?.ToString() ?? "Unknown",
                                Status = reader["status"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"로그 불러오기 실패: {ex.Message}");
            }
            return list;
        }
    }
}