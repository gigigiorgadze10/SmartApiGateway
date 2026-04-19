using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartApiGateway.Data;

namespace SmartApiGateway.Models
{
    public class ApiEndpoint
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "შემომავალი მარშრუტი")]
        public string RoutePath { get; set; } = string.Empty;

        [Required]
        [Display(Name = "სამიზნე URL")]
        public string TargetUrl { get; set; } = string.Empty;

        [Display(Name = "აქტიურია?")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "აღწერა")]
        public string Description { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ვინ შექმნა ეს მარშრუტი?
        public string? CreatedById { get; set; }

        [ForeignKey("CreatedById")]
        public ApplicationUser? CreatedBy { get; set; }

        public string[] GetTargetUrls() =>
            TargetUrl.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}