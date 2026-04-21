using System.Collections.Generic;

namespace SmartApiGateway.ViewModels
{
    public class ManageRolePermissionsViewModel
    {
        public string RoleId { get; set; }
        public string RoleName { get; set; }
        public IList<RoleClaimViewModel> RoleClaims { get; set; } = new List<RoleClaimViewModel>();
    }

    public class RoleClaimViewModel
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public bool Selected { get; set; }
    }
}