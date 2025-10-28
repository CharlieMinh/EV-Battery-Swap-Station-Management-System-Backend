using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.Complaints;

public class InvestigateComplaintRequest
{
    [StringLength(1000)]
    public string? InvestigationNotes { get; set; }
}
