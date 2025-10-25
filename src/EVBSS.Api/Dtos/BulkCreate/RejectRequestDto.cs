using System.ComponentModel.DataAnnotations;

namespace EVBSS.Api.Dtos.BulkCreate
{
    public class RejectRequestDto
    {
        [Required]
        public string Notes { get; set; }
    }
}