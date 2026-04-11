using System.Collections.Generic;

namespace SmartApiGateway.ViewModels
{
    public class EditUserViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IList<string> CurrentRoles { get; set; } = new List<string>();
        public List<string> AvailableRoles { get; set; } = new List<string>();
        public string SelectedRole { get; set; } = string.Empty;
    }
}