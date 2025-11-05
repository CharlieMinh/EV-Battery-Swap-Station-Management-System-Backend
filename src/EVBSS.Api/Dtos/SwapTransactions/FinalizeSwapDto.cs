namespace EVBSS.Api.Dtos.SwapTransactions;

public class FinalizeSwapRequest
{
    public Guid ReservationId { get; set; }
    // Optional: health percentage of the old battery returned by the customer/staff
    public int? OldBatteryHealth { get; set; }
    // Optional: notes from staff about the swap transaction
    public string? Notes { get; set; }
    // public object? BatteryCheckStats { get; set; } // Optional: For future use
}

public class FinalizeSwapResponse
{
    public bool Success { get; set; }
    public Guid SwapTransactionId { get; set; }
    public string Message { get; set; } = null!;
    public string? Notes { get; set; }
}
