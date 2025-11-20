using System.Collections.Generic;
using Newtonsoft.Json; // 필수: NuGet에서 설치한 패키지

namespace AMPManager.Model
{
    public class ServerData
    {
        // 파이썬의 "allocation_count"를 C#의 AllocationCount로 연결
        [JsonProperty("allocation_count")]
        public int AllocationCount { get; set; }

        [JsonProperty("current_complete")]
        public int CurrentComplete { get; set; }

        [JsonProperty("defect_rate")]
        public double DefectRate { get; set; }

        [JsonProperty("logs")]
        public List<string> Logs { get; set; } = new List<string>();
    }
}