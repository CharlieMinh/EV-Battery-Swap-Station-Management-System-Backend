# Hướng dẫn tích hợp Re-swap với BatteryComplaint

Tài liệu ngắn này mô tả cách sử dụng trường `RelatedComplaintId` trên `SwapTransaction` để liên kết một giao dịch re-swap (đổi pin miễn phí do khiếu nại được xác nhận) về khiếu nại gốc (`BatteryComplaint`).

Mục tiêu
- Khi Staff xác nhận khiếu nại (Confirmed), hệ thống sẽ cấp một lượt đổi pin miễn phí (re-swap).
- Giao dịch re-swap phải tham chiếu về khiếu nại gốc để dễ tra cứu và hoàn tất tự động.

Thiết kế dữ liệu
- `SwapTransaction.RelatedComplaintId` (nullable GUID): trỏ tới `BatteryComplaint.Id` nếu giao dịch là re-swap được tạo do khiếu nại.
- FK đã tạo trong DB có `ON DELETE NO ACTION` (không tự động cascade) — điều này tránh lỗi "multiple cascade paths" trên SQL Server. Vì vậy việc xóa bản ghi complaint phải được cân nhắc.

Luồng xử lý (ví dụ thực hiện re-swap)

1) Staff xác nhận khiếu nại

- Staff gọi API resolve với `NewStatus = ComplaintStatus.Confirmed`.
- Ở trạng thái này, service có thể tạo một reservation hoặc một swap transaction re-swap.

2) Tạo giao dịch re-swap (SwapTransaction)

Ví dụ C# (tạo SwapTransaction re-swap trực tiếp):

```csharp
// Giả sử bạn đã có complaint (BatteryComplaint complaint)
var reSwap = new SwapTransaction
{
    Id = Guid.NewGuid(),
    TransactionNumber = GenerateTransactionNumber(),
    UserId = complaint.ReportedByUserId,
    StationId = stationId,
    VehicleId = vehicleId,
    IssuedBatteryId = newIssuedBatteryId,
    IssuedBatterySerial = newIssuedBatterySerial,
    Status = SwapTransactionStatus.Completed, // Hoặc Pending/CheckedIn tuỳ luồng
    SwapFee = 0m, // Miễn phí cho re-swap
    RelatedComplaintId = complaint.Id // Liên kết ngược về khiếu nại
};

dbContext.SwapTransactions.Add(reSwap);
await dbContext.SaveChangesAsync();
```

Ghi chú:
- Bạn có thể tạo reservation trước, cho user đến trạm và sau đó tạo SwapTransaction khi thực hiện xong. Điều quan trọng là `RelatedComplaintId` được set cho SwapTransaction re-swap.

3) Hoàn tất khiếu nại (Finalize)

- Sau khi SwapTransaction re-swap được hoàn tất (status `Completed`), gọi `FinalizeComplaintAsync(staffId, complaint.Id)` để chuyển `BatteryComplaint.Status` sang `Resolved`.

Ví dụ đơn giản:

```csharp
// Trong service hoặc controller khi swap hoàn thành
if (reSwap.RelatedComplaintId.HasValue && reSwap.Status == SwapTransactionStatus.Completed)
{
    await _batteryComplaintService.FinalizeComplaintAsync(staffId, reSwap.RelatedComplaintId.Value);
}
```

Kiểm tra DB (SQL)

Bạn có thể xác thực cột và constraint bằng các truy vấn sau (SQL Server):

```sql
-- Kiểm tra cột
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'SwapTransactions' AND COLUMN_NAME = 'RelatedComplaintId';

-- Kiểm tra index
SELECT name, object_id
FROM sys.indexes
WHERE name = 'IX_SwapTransactions_RelatedComplaintId';

-- Kiểm tra foreign key
SELECT fk.name, OBJECT_NAME(fk.parent_object_id) AS table_name, fk.delete_referential_action_desc
FROM sys.foreign_keys fk
WHERE fk.name LIKE 'FK_SwapTransactions%';

-- Kiểm tra migrations history
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;
```

Lưu ý & Lời khuyên
- Hiện FK `RelatedComplaintId` được tạo với `ON DELETE NO ACTION` để tránh lỗi multiple cascade paths.
- Nếu bạn muốn FK dùng `ON DELETE SET NULL`, ta cần điều chỉnh các FK khác trên `SwapTransactions` để loại bỏ multiple cascade paths — điều này yêu cầu thay đổi migration và kiểm tra kỹ trên DB.
- Tránh xóa `BatteryComplaint` trực tiếp nếu bạn muốn giữ lịch sử re-swap liên quan; thay vào đó, sử dụng business logic để mark là archived hoặc resolved.

Next steps (tùy bạn chọn)
- Nếu bạn muốn tôi thêm helper/service mẫu để tự động tạo re-swap và finalize khi swap completed, tôi có thể cài đặt vào `BatteryComplaintService` và thêm unit tests kèm ví dụ.
- Nếu bạn muốn đổi FK thành `ON DELETE SET NULL`, tôi sẽ phân tích các FK khác và đề xuất migration an toàn.

---
Tài liệu này ngắn gọn, mục tiêu là giúp dev hiểu cách dùng `RelatedComplaintId` và những rủi ro DB liên quan.
