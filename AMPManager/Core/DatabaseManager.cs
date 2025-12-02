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

        // 1. 로그인
        public User? Login(string id, string pw)
        {
            if (id == "admin" && pw == "1234") return new User("김관리", "admin", 1);
            if (id == "worker" && pw == "1234") return new User("이작업", "worker", 2);
            return null;
        }

        // 2. 테이블 구조 생성
        private void EnsureTableStructure()
        {
            if (!File.Exists("factory.db")) return;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    // 필요한 컬럼들이 없으면 추가
                    var cols = new[] { "img_cam1", "img_cam2", "result" };
                    foreach (var col in cols)
                    {
                        try
                        {
                            using (SQLiteCommand cmd = new SQLiteCommand($"ALTER TABLE MEASUREMENTS ADD COLUMN {col} BLOB", conn))
                                cmd.ExecuteNonQuery();
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // 3. 측정 데이터 저장 (실시간용)
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
                        cmd.Parameters.AddWithValue("@img1", img1 ?? new byte[0]);
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

        // 4. 로그 조회
        public List<LogEntry> GetLogs(string targetDate)
        {
            var list = new List<LogEntry>();
            if (!File.Exists("factory.db")) return list;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT M.MID, M.measurement_time, P.name, M.result
                        FROM MEASUREMENTS M
                        LEFT JOIN PRODUCT P ON M.productID = P.PID
                        WHERE M.measurement_time LIKE @date || '%'
                        ORDER BY M.MID DESC";

                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@date", targetDate);
                        using (SQLiteDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string res = reader["result"].ToString();
                                if (string.IsNullOrEmpty(res)) res = "OK";

                                list.Add(new LogEntry
                                {
                                    Id = Convert.ToInt32(reader["MID"]),
                                    Timestamp = reader["measurement_time"].ToString(),
                                    PropertyName = reader["name"].ToString(),
                                    Status = res == "NG" ? "불량" : "정상"
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        // 5. 이미지 가져오기
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

        // =========================================================
        // [수정된 부분] 폴더 사진 DB 일괄 저장 (에러 메시지 출력 기능 추가)
        // =========================================================
        public void ImportImagesFromFolder()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dirCam1 = Path.Combine(baseDir, "topcamera");
            string dirCam2 = Path.Combine(baseDir, "bottomcamera");

            // 폴더가 없으면 경고창 띄우기
            if (!Directory.Exists(dirCam1))
            {
                System.Windows.MessageBox.Show($"[오류] topcamera 폴더를 찾을 수 없습니다!\n경로: {dirCam1}");
                return;
            }

            var files = Directory.GetFiles(dirCam1, "cam1_*.png");
            if (files.Length == 0)
            {
                System.Windows.MessageBox.Show($"[알림] topcamera 폴더에 'cam1_...' 로 시작하는 파일이 없습니다.");
                return;
            }

            using (SQLiteConnection conn = new SQLiteConnection(ConnectionString))
            {
                conn.Open();
                foreach (var file1 in files)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file1);
                        string datePart = fileName.Substring(5); // "20251127_112950"

                        DateTime dt = DateTime.ParseExact(datePart, "yyyyMMdd_HHmmss", null);
                        string dbTime = dt.ToString("yyyy-MM-dd HH:mm:ss");

                        byte[] img1Bytes = File.ReadAllBytes(file1);
                        byte[] img2Bytes = new byte[0];

                        for (int i = 0; i <= 3; i++)
                        {
                            DateTime targetTime = dt.AddSeconds(i);
                            string targetName = $"cam2_{targetTime:yyyyMMdd_HHmmss}.png";
                            string targetPath = Path.Combine(dirCam2, targetName);
                            if (File.Exists(targetPath))
                            {
                                img2Bytes = File.ReadAllBytes(targetPath);
                                break;
                            }
                        }

                        string query = "INSERT INTO MEASUREMENTS (productID, measurement_time, result, img_cam1, img_cam2) VALUES (1, @time, 'OK', @img1, @img2)";
                        using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@time", dbTime);
                            cmd.Parameters.AddWithValue("@img1", img1Bytes);
                            cmd.Parameters.AddWithValue("@img2", img2Bytes);
                            cmd.ExecuteNonQuery();
                        }
                        System.Diagnostics.Debug.WriteLine($"[저장성공] {dbTime}");
                    }
                    catch (Exception ex)
                    {
                        // ★★★ 여기가 수정된 부분입니다 ★★★
                        // 에러가 나면 숨기지 말고 화면에 띄웁니다!
                        System.Windows.MessageBox.Show($"저장 실패!\n파일: {Path.GetFileName(file1)}\n이유: {ex.Message}");
                    }
                }
            }
        }

        // 7. 통계 함수 (껍데기)
        public Dictionary<string, double> GetDailyDefectRates(DateTime start, DateTime end)
        {
            return new Dictionary<string, double>();
        }

        public (double w, double l, double c, double cp) GetAverageSpecs()
        {
            return (0, 0, 0, 0);
        }
    }
}