using EVBSS.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Complaints;

public class ResolveComplaintRequest
{
    [Required]
    public ComplaintStatus NewStatus { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "Mô tả giải quyết phải từ 10 đến 500 ký tự")]
    public string ResolutionNotes { get; set; } = null!; // details about investigation and decision
}
