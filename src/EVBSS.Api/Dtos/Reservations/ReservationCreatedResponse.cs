using System;

namespace EVBSS.Api.Dtos.Reservations;

public record ReservationCreatedResponse(Guid Id, string Status, DateTime ExpiresAt);
