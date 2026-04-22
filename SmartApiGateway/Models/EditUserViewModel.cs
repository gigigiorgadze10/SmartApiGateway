using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.Models
{
    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "სახელის მითითება სავალდებულოა")]
        [Display(Name = "მომხმარებლის სახელი")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "ელ-ფოსტის მითითება სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ელ-ფოსტის ფორმატი")]
        [Display(Name = "ელ-ფოსტა")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "როლი")]
        public string? SelectedRole { get; set; }

        // ეს არის ის ლისტი, რომელსაც Controller-ში ვითხოვთ და ერორს აგდებდა
        public IList<string> Roles { get; set; } = new List<string>();
    }
}