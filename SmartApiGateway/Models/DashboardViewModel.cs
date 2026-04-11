using SmartApiGateway.Models;
using System.Collections.Generic;

namespace SmartApiGateway.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalRequests { get; set; }
        public int BlockedIpsCount { get; set; }
        public double AverageResponseTime { get; set; }
        public List<TrafficLog> RecentLogs { get; set; } = new(); // ინიციალიზაცია
    }
}