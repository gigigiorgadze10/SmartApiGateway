using System.Collections.Generic;

namespace SmartApiGateway.Models
{
    public class DashboardViewModel
    {
        public int TotalRequests { get; set; }
        public int BlockedIpsCount { get; set; }
        public double AverageResponseTime { get; set; }
        public List<TrafficLog> RecentLogs { get; set; } = new();

        // ახალი: ფილტრი
        public string ActiveFilter { get; set; } = "24h";

        // ახალი: ჩარტების ისტორიული მონაცემები
        public List<string> ChartLabels { get; set; } = new();
        public List<long> ChartData { get; set; } = new();

        // ახალი: გამოყოფილი სტატუს კოდები
        public int SuccessCount { get; set; } // 2xx
        public int ClientErrorCount { get; set; } // 4xx (მაგ: 404, 403, 429)
        public int ServerErrorCount { get; set; } // 5xx (მაგ: 500, 502)

        // ახალი: Top IP-ების სტატისტიკა
        public Dictionary<string, int> TopIps { get; set; } = new();
    }
}