using System.Collections.Generic;

namespace SmartApiGateway.ViewModels
{
    public class ManageRolePermissionsViewModel
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public IList<RoleClaimViewModel> RoleClaims { get; set; } = new List<RoleClaimViewModel>();
    }

    public class RoleClaimViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
}