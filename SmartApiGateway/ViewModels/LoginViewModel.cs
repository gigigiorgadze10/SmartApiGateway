using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "ელ-ფოსტა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ფორმატი")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლი სავალდებულოა")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}