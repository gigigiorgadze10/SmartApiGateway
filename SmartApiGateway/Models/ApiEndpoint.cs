using System;
using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.Models
{
    public class ApiEndpoint
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "შემომავალი მარშრუტი (მაგ. /api/users)")]
        public string RoutePath { get; set; } = string.Empty;

        [Required]
        [Display(Name = "სამიზნე URL (მაგ. https://api.example.com/users)")]
        public string TargetUrl { get; set; } = string.Empty;

        [Display(Name = "აქტიურია?")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "აღწერა")]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}