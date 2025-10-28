# Hướng dẫn kiểm thử luồng Khiếu nại + Đổi pin lỗi trên Swagger UI

Tập trung: kiểm thử từng bước của luồng "Driver báo pin lỗi" → Staff điều tra → Staff xác nhận → Staff thu pin lỗi & tạo lượt đổi pin miễn phí (re-swap) → Hoàn tất re-swap → Complaint tự động được đóng (Auto-Finalize).

Môi trường: ứng dụng chạy local, Swagger UI có thể truy cập tại `http://localhost:{port}/swagger`.

Ghi chú quan trọng trước khi bắt đầu
- Nếu `SwapTransaction.RelatedComplaintId` không được set cho giao dịch re-swap, tính năng auto-finalize sẽ KHÔNG chạy. Flow tạo re-swap mặc định trong code có thể không set trường này tự động; bạn có thể set nó thủ công (SQL) hoặc sửa flow để set trường này. Hướng dẫn cách set phía dưới.
- Yêu cầu: một tài khoản Driver và một tài khoản Staff (role = Staff). Dùng endpoint đăng ký/đăng nhập để tạo/đăng nhập tài khoản.

## 1) Đăng nhập lấy JWT (Driver & Staff)

1. Mở Swagger UI.
2. Tìm `POST /api/v1/Auth/login`.
3. Gửi body mẫu để đăng nhập (Driver):

```json
{
  "email": "driver@example.com",
  "password": "driver-password"
}
```

4. Copy giá trị `token` trong response.
5. Nhấn nút "Authorize" ở góc trên cùng của Swagger và dán: `Bearer {token}` (ví dụ: `Bearer eyJhbGci...`).

Lưu ý: API cũng set cookie `jwt` khi login; Swagger sẽ sử dụng header Authorization nếu bạn dán token vào Authorize.

## 2) Bước 1 — Driver báo pin lỗi (Report)

Endpoint: `POST /api/BatteryComplaints` ? (nếu bạn đã expose Report endpoint khác, dùng endpoint tương ứng). Trong repository hiện tại chức năng report dùng DTO `ReportFaultyBatteryRequest` và service `ReportFaultyBatteryAsync`.

- Body mẫu (driver đang đăng nhập):

```json
{
  "swapTransactionId": "<swap-transaction-id>",
  "complaintDetails": "Pin mất công suất rất nhanh, xuống dưới 40% chỉ sau 1 lần sạc."
}
```

Expected:
- 200/201 response chứa `complaint.Id`.
- Bản ghi `BatteryComplaints` mới với Status = `Pending`.
- SignalR notification được gửi cho group `Staff` (nếu client đang subscribe).

Kiểm tra nhanh (SQL):
```sql
SELECT TOP 1 * FROM BatteryComplaints WHERE ReportedByUserId = '<driver-id>' ORDER BY ReportDate DESC;
```

## 3) Bước 2 — Staff khảo sát (Investigate)

Endpoint: `POST /api/BatteryComplaints/{id}/investigate`
- Chỉ Staff/Admin mới gọi được. Đảm bảo bạn đã đăng nhập bằng tài khoản Staff và dán token trong Authorize.

Body mẫu:
```json
{
  "investigationNotes": "Đã kiểm tra pin tại trạm, quan sát mạch & kết quả đo: SOC giảm nhanh."
}
```

Expected:
- Complaint.Status -> `Investigating`
- HandledByStaffId được set = staffId

## 4) Bước 3 — Staff giải quyết (Confirm / Reject)

Endpoint: `POST /api/BatteryComplaints/{id}/resolve`

Body mẫu (Confirm):
```json
{
  "newStatus": 2, // ComplaintStatus.Confirmed
  "resolutionNotes": "Xác nhận: Pin bị lỗi theo tiêu chuẩn kiểm định nội bộ."
}
```

- Nếu `newStatus` = Confirmed (2) thì hệ thống:
  - Đánh dấu battery `IssuedBattery` là `Faulty` và cập nhật inventory.
  - Tạo notification thông báo cho Driver.

- Nếu `newStatus` = Rejected (3) thì thông báo từ chối.

SQL kiểm tra:
```sql
SELECT Status, HandledByStaffId, ResolvedAt FROM BatteryComplaints WHERE Id = '<complaint-id>';
SELECT Status FROM BatteryUnits WHERE Id = '<issued-battery-id>';
```

## 5) Bước 4 — Staff thu pin lỗi & tạo re-swap miễn phí

Endpoint trong repository: `POST /api/BatteryComplaints/{id}/receive-faulty-battery` (method `ProcessFaultyBatteryReturnAndCreateReswapAsync` trên service). Gọi endpoint này bằng Staff.

Expected:
- Tạo 1 `Reservation` mới (trạng thái Pending) dành cho user báo khiếu nại.
- Complaint sẽ được set `Status = Resolved` bởi method hiện tại — lưu ý: code hiện tại đánh dấu Resolved ngay khi tạo Reservation (business logic chosen). Nếu bạn muốn khác (ví dụ: Resolved khi re-swap hoàn tất), cần điều chỉnh logic.

Response sample:
```json
{
  "message": "Pin lỗi đã được thu hồi và một lượt đổi pin miễn phí đã được tạo.",
  "reservation": { "id": "...", "stationId": "..." }
}
```

## 6) Bước 5 — Thực hiện re-swap (Staff/Driver flow)

Kịch bản cho re-swap:
- Driver đến trạm đã được tạo reservation. Staff thực hiện quy trình IssueBattery/ReceiveBattery/Finalize (tùy triển khai).
- Nếu bạn dùng endpoint `FinalizeFromReservationAsync` (hoàn tất swap từ reservation), swap mới sẽ được tạo tại cuối bước này.

Quan trọng để kiểm thử Auto-Finalize:
- `SwapTransaction.RelatedComplaintId` PHẢI được set = `<complaint.Id>` trên SwapTransaction re-swap.
- Trong repo hiện tại, `ProcessFaultyBatteryReturnAndCreateReswapAsync` chỉ tạo Reservation; nó chưa tự động set RelatedComplaintId trên SwapTransaction vì SwapTransaction chỉ được tạo khi finalize swap. Do đó để test auto-finalize bạn có 2 cách:
  1) Manual DB step (dành cho test): Sau khi re-swap được tạo và có SwapTransaction.Id, chạy SQL cập nhật:

```sql
UPDATE SwapTransactions
SET RelatedComplaintId = '<complaint-id>'
WHERE Id = '<re-swap-swaptransaction-id>'
```

  2) (Tốt hơn) Extend the flow: khi tạo SwapTransaction từ reservation, set `RelatedComplaintId = reservation.CreatedFromComplaintId` hoặc cung cấp API option để set RelatedComplaintId; (yêu cầu thay đổi code).

## 7) Bước 6 — Kiểm tra Auto-Finalize

Sau khi SwapTransaction (re-swap) đã `Status = Completed` và `RelatedComplaintId` trỏ tới complaint:
- `SwapTransactionService` đã được mở rộng để gọi `BatteryComplaintService.FinalizeComplaintAsync` nếu complaint.Status == Confirmed.
- Kết quả: Complaint.Status sẽ chuyển sang `Resolved` (nếu trước đó là `Confirmed`).

SQL kiểm tra:
```sql
SELECT Id, Status, RelatedComplaintId FROM SwapTransactions WHERE Id = '<re-swap-id>';
SELECT Id, Status, HandledByStaffId, ResolvedAt FROM BatteryComplaints WHERE Id = '<complaint-id>';
SELECT * FROM Notifications WHERE RelatedEntityId = '<complaint-id>' ORDER BY CreatedAt DESC;
```

## Quick troubleshooting

- Auto-finalize không chạy
  - Kiểm tra `RelatedComplaintId` đã được set cho SwapTransaction chưa.
  - Kiểm tra complaint.Status có phải `Confirmed` không. Auto-finalize chỉ chạy khi complaint đã Confirmed.

- Không thể gọi các endpoint Staff
  - Đảm bảo token dùng cho "Authorize" là tài khoản có Role = Staff hoặc Admin.
  - Kiểm tra claim role trong JWT bằng endpoint `GET /api/v1/Auth/me`.

- Thông báo Real-time (SignalR) không nhận được
  - Kiểm tra client đã subscribe group `Staff` không.
  - Server gửi sự kiện `ReceiveComplaint` vào group "Staff".

## Tóm tắt quick-test (một kịch bản nhỏ để chạy nhanh)
1. Login driver -> lấy token.
2. Login staff -> lấy token (mở 2 tab hoặc copy token khi cần thay đổi Authorization trong Swagger).
3. Driver gọi `ReportFaultyBattery` với `swapTransactionId` hợp lệ.
4. Staff gọi `POST /api/BatteryComplaints/{id}/investigate`.
5. Staff gọi `POST /api/BatteryComplaints/{id}/resolve` với `newStatus = 2` (Confirmed).
6. Staff gọi `POST /api/BatteryComplaints/{id}/receive-faulty-battery` để tạo reservation (re-swap).
7. Hoàn tất re-swap (Issue/Receive/Finalize) — sau khi SwapTransaction mới được tạo, đảm bảo `RelatedComplaintId` trỏ về khiếu nại.
8. Kiểm tra Complaint.Status đã chuyển sang `Resolved` và kiểm tra Notifications.

---

Nếu bạn muốn, tôi có thể:
- Thêm các `Example Value` trực tiếp vào Swagger bằng cách chỉnh attribute `[Produces]` hoặc `SwaggerGen` để các body mẫu hiện sẵn ở UI.
- Tạo một endpoint test helper (dev-only) để tự động set `RelatedComplaintId` trên một swap để test auto-finalize mà không cần sửa DB.

Bạn muốn tôi: 1) lưu file này vào repo (tôi đã tạo `docs/SWAGGER_TEST_COMPLAINT_FLOW.md`), hay 2) bổ sung helper endpoint / unit-tests để tự động hóa kiểm thử?
