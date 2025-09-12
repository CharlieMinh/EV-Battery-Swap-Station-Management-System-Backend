namespace EVBSS.Api.Models;

public class Station
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string City { get; set; } = "HCM";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public bool IsActive { get; set; } = true;
}
