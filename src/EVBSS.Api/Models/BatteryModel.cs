namespace EVBSS.Api.Models;

public class BatteryModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public int Voltage { get; set; }          // V
    public int CapacityWh { get; set; }       // Wh
    public string? Manufacturer { get; set; }
    
    /// <summary>
    /// Giá đổi pin 1 lần cho loại pin này (Pay-per-Swap pricing)
    /// VD: VF3 (30kWh) = 50,000 VNĐ/lần, VF8 (87kWh) = 120,000 VNĐ/lần
    /// Frontend sẽ query giá này trước khi user đặt lịch lẻ
    /// </summary>
    public decimal SwapPricePerSession { get; set; } = 0;
}
