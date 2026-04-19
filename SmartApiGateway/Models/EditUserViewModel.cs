using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.Models
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "სახელის მითითება სავალდებულოა")]
        [Display(Name = "მომხმარებლის სახელი")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "ელ. ფოსტა")]
        public string Email { get; set; } = string.Empty; // ელ.ფოსტას ვაჩვენებთ, მაგრამ რედაქტირებას ავუკრძალავთ UI-ში

        [Display(Name = "წვდომის როლი")]
        public string? SelectedRole { get; set; }

        public IEnumerable<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();
    }
}