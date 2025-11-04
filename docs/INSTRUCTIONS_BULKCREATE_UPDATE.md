# 📝 HƯỚNG DẪN BỔ SUNG CODE CHO BULKCREATEREQUESTSCONTROLLER

## Vị trí: Trong phương thức `ConfirmRequest` sau dòng `await transaction.CommitAsync();`

Tìm đoạn code sau (khoảng dòng 513):
```csharp
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // NOTIFICATION STEP: Send result back to all Admins
```

Thay thế bằng:
```csharp
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ⭐ NEW: Cập nhật trạng thái BatteryStockRequest thành Completed (nếu có)
                try
                {
                    await _stockRequestService.CompleteStockRequestAsync(request.Id);
                    _logger.LogInformation("✅ Related BatteryStockRequest completed for BulkCreateRequest {RequestId}", request.Id);
                }
                catch (Exception stockEx)
                {
                    // Log nhưng không rollback transaction chính
                    _logger.LogWarning(stockEx, "Failed to complete related BatteryStockRequest for BulkCreateRequest {RequestId}", request.Id);
                }

                // NOTIFICATION STEP: Send result back to all Admins
```

## Thêm Service vào Constructor

Tìm constructor (khoảng dòng 46):
```csharp
        public BulkCreateRequestsController(AppDbContext context, ILogger<BulkCreateRequestsController> logger, IHubContext<NotificationHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }
```

Thay thế bằng:
```csharp
        private readonly IBatteryStockRequestService _stockRequestService; // ⭐ ADD THIS

        public BulkCreateRequestsController(
            AppDbContext context, 
            ILogger<BulkCreateRequestsController> logger, 
            IHubContext<NotificationHub> hubContext,
            IBatteryStockRequestService stockRequestService) // ⭐ ADD THIS PARAMETER
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
            _stockRequestService = stockRequestService; // ⭐ ADD THIS
        }
```

## Thêm using statement ở đầu file

Thêm dòng này vào phần using (khoảng dòng 6):
```csharp
using EVBSS.Api.Services; // ⭐ ADD THIS LINE
```
