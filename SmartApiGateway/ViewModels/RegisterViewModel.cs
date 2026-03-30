using System.ComponentModel.DataAnnotations;

namespace SmartApiGateway.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "სახელი სავალდებულოა")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "გვარი სავალდებულოა")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "ელ-ფოსტა სავალდებულოა")]
        [EmailAddress(ErrorMessage = "არასწორი ფორმატი")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "პაროლი სავალდებულოა")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "პაროლები არ ემთხვევა")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}