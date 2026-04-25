using System.Collections.Generic;

namespace SmartApiGateway.Models
{
    public class DashboardViewModel
    {
        // დაამატეთ ეს ხაზი
        public int TotalRequests { get; set; }
        public int BlockedIpsCount { get; set; }
        public double AverageResponseTime { get; set; }
        public List<TrafficLog> RecentLogs { get; set; } = new();

        public string ActiveFilter { get; set; } = "24h";
        public int EndpointLimit { get; set; } = 10;

        public List<string> ChartLabels { get; set; } = new();
        public List<long> ChartData { get; set; } = new();

        public int SuccessCount { get; set; }
        public int ClientErrorCount { get; set; }
        public int ServerErrorCount { get; set; }

        public Dictionary<string, int> TopIps { get; set; } = new();

        // ახალი: ენდპოინტების სტატისტიკა
        public List<EndpointStat> EndpointStats { get; set; } = new();
    }

    // დამხმარე კლასი ენდპოინტებისთვის
    public class EndpointStat
    {
        public string Path { get; set; } = string.Empty;
        public int SuccessCount { get; set; }
        public int ClientErrorCount { get; set; } // 4xx (ახალი)
        public int ServerErrorCount { get; set; } // 5xx (ახალი)
        public int TotalCount { get; set; }
    }
}