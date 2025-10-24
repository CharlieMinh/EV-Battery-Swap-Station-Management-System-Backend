namespace EVBSS.Api.Services;

public class PaymentPendingCashException : Exception
{
    public Guid PaymentId { get; }
    public decimal Amount { get; }

    public PaymentPendingCashException(string message, Guid paymentId, decimal amount) : base(message)
    {
        PaymentId = paymentId;
        Amount = amount;
    }
}

public class ActiveReservationExistsException : Exception
{
    public ActiveReservationExistsException(string message) : base(message) { }
}

public class SlotNotAvailableException : Exception
{
    public SlotNotAvailableException(string message) : base(message) { }
}

public class NoActiveSubscriptionException : Exception
{
    public NoActiveSubscriptionException(string message) : base(message) { }
}

public class InvalidCheckInTimeException : Exception
{
    public InvalidCheckInTimeException(string message) : base(message) { }
}

public class NoBatteryException : Exception
{
    public NoBatteryException(string message) : base(message) { }
}
