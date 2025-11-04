# 🧪 HƯỚNG DẪN TEST API - LUỒNG YÊU CẦU TĂNG PIN

## 📋 MỤC LỤC
1. [Chuẩn bị](#chuẩn-bị)
2. [Test Case 1: Staff tạo yêu cầu](#test-case-1-staff-tạo-yêu-cầu)
3. [Test Case 2: Admin xem và duyệt yêu cầu](#test-case-2-admin-xem-và-duyệt-yêu-cầu)
4. [Test Case 3: Staff xác nhận nhận pin](#test-case-3-staff-xác-nhận-nhận-pin)
5. [Test Case 4: Admin từ chối yêu cầu](#test-case-4-admin-từ-chối-yêu-cầu)
6. [Kiểm tra Database](#kiểm-tra-database)
7. [Kiểm tra Notifications](#kiểm-tra-notifications)

---

## 🔧 CHUẨN BỊ

### 1. Khởi động Backend
```powershell
cd D:\SWP391\BE\EV-Battery-Swap-Station-Management-System-Backend\src\EVBSS.Api
dotnet run
```

Backend sẽ chạy tại: `https://localhost:7001` hoặc `http://localhost:5001`

### 2. Tool để test
- **Postman** (Khuyến nghị)
- **Thunder Client** (VS Code Extension)
- **cURL** (Command line)

### 3. Lấy Token đăng nhập

#### 🔑 Login Staff
```http
POST https://localhost:7001/api/v1/auth/login
Content-Type: application/json

{
  "email": "staff1@evbss.local",
  "password": "staff123"
}
```

**Lưu lại:** `STAFF_TOKEN` từ response

#### 🔑 Login Admin
```http
POST https://localhost:7001/api/v1/auth/login
Content-Type: application/json

{
  "email": "admin@evbss.local",
  "password": "12345678Swp@"
}
```

**Lưu lại:** `ADMIN_TOKEN` từ response

### 4. Lấy thông tin cần thiết

#### Lấy danh sách Stations
```http
GET https://localhost:7001/api/v1/stations
Authorization: Bearer {{STAFF_TOKEN}}
```

**Lưu lại:** `stationId` (ví dụ: của trạm "Trạm Gò Vấp")

#### Lấy danh sách Battery Models
```http
GET https://localhost:7001/api/v1/battery-models
Authorization: Bearer {{STAFF_TOKEN}}
```

**Lưu lại:** `batteryModelId` (ví dụ: "VinFast VF8 60kWh")

---

## ✅ TEST CASE 1: STAFF TẠO YÊU CẦU

### Bước 1: Staff tạo yêu cầu tăng pin

```http
POST https://localhost:7001/api/v1/staff/stock-requests
Authorization: Bearer {{STAFF_TOKEN}}
Content-Type: application/json

{
  "stationId": "{{stationId}}",
  "batteryModelId": "{{batteryModelId}}",
  "quantity": 15,
  "staffNote": "Tuần sau dự kiến có nhiều khách hàng, cần bổ sung pin"
}
```

### ✅ Expected Response (201 Created)
```json
{
  "message": "✅ Yêu cầu tăng pin đã được gửi đến Admin.",
  "request": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "stationId": "{{stationId}}",
    "batteryModelId": "{{batteryModelId}}",
    "quantity": 15,
    "staffNote": "Tuần sau dự kiến có nhiều khách hàng, cần bổ sung pin",
    "status": "PendingAdminReview",
    "requestedByStaffId": "{{staffUserId}}",
    "requestDate": "2025-11-02T14:30:00Z",
    "updatedAt": "2025-11-02T14:30:00Z",
    "adminReviewerId": null,
    "adminReviewDate": null,
    "adminNote": null,
    "relatedBulkCreateRequestId": null
  }
}
```

**🔖 LƯU LẠI:** `requestId` để dùng cho các bước sau

### Bước 2: Staff xem yêu cầu vừa tạo

```http
GET https://localhost:7001/api/v1/staff/stock-requests/{{requestId}}
Authorization: Bearer {{STAFF_TOKEN}}
```

### ✅ Expected Response (200 OK)
- Trả về thông tin request vừa tạo
- `status` phải là `"PendingAdminReview"`

### Bước 3: Staff xem tất cả yêu cầu của mình

```http
GET https://localhost:7001/api/v1/staff/stock-requests/mine
Authorization: Bearer {{STAFF_TOKEN}}
```

### ✅ Expected Response (200 OK)
```json
[
  {
    "id": "{{requestId}}",
    "stationId": "{{stationId}}",
    "stationName": "Trạm Gò Vấp",
    "batteryModelId": "{{batteryModelId}}",
    "batteryModelName": "VinFast VF8 60kWh",
    "quantity": 15,
    "staffNote": "Tuần sau dự kiến có nhiều khách hàng, cần bổ sung pin",
    "status": "PendingAdminReview",
    "requestedByStaffId": "{{staffUserId}}",
    "requestedByStaffName": "Staff User 1",
    "requestDate": "2025-11-02T14:30:00Z",
    "updatedAt": "2025-11-02T14:30:00Z"
  }
]
```

### ❌ Test Error Cases

#### 1. Quantity = 0 hoặc âm
```http
POST https://localhost:7001/api/v1/staff/stock-requests
Authorization: Bearer {{STAFF_TOKEN}}
Content-Type: application/json

{
  "stationId": "{{stationId}}",
  "batteryModelId": "{{batteryModelId}}",
  "quantity": 0,
  "staffNote": "Test lỗi"
}
```

**Expected:** 400 Bad Request - "Số lượng pin phải lớn hơn 0."

#### 2. StationId không tồn tại
```http
POST https://localhost:7001/api/v1/staff/stock-requests
Authorization: Bearer {{STAFF_TOKEN}}
Content-Type: application/json

{
  "stationId": "00000000-0000-0000-0000-000000000000",
  "batteryModelId": "{{batteryModelId}}",
  "quantity": 10,
  "staffNote": "Test lỗi"
}
```

**Expected:** 400 Bad Request - "Trạm không tồn tại."

#### 3. Staff không được phân công tại trạm
```http
POST https://localhost:7001/api/v1/staff/stock-requests
Authorization: Bearer {{STAFF_TOKEN_FROM_ANOTHER_STATION}}
Content-Type: application/json

{
  "stationId": "{{stationId}}",
  "batteryModelId": "{{batteryModelId}}",
  "quantity": 10,
  "staffNote": "Test lỗi"
}
```

**Expected:** 403 Forbidden - "Bạn không được phân công tại trạm này."

---

## ✅ TEST CASE 2: ADMIN XEM VÀ DUYỆT YÊU CẦU

### Bước 1: Admin xem danh sách yêu cầu chờ duyệt

```http
GET https://localhost:7001/api/v1/admin/stock-requests/pending
Authorization: Bearer {{ADMIN_TOKEN}}
```

### ✅ Expected Response (200 OK)
```json
[
  {
    "id": "{{requestId}}",
    "stationId": "{{stationId}}",
    "stationName": "Trạm Gò Vấp",
    "batteryModelId": "{{batteryModelId}}",
    "batteryModelName": "VinFast VF8 60kWh",
    "quantity": 15,
    "staffNote": "Tuần sau dự kiến có nhiều khách hàng, cần bổ sung pin",
    "status": "PendingAdminReview",
    "requestedByStaffId": "{{staffUserId}}",
    "requestedByStaffName": "Staff User 1",
    "requestDate": "2025-11-02T14:30:00Z",
    "updatedAt": "2025-11-02T14:30:00Z"
  }
]
```

### Bước 2: Admin xem chi tiết yêu cầu

```http
GET https://localhost:7001/api/v1/admin/stock-requests/{{requestId}}
Authorization: Bearer {{ADMIN_TOKEN}}
```

### ✅ Expected Response (200 OK)
- Trả về thông tin đầy đủ của request
- Có thêm các navigation properties (Station, BatteryModel, RequestedByStaff)

### Bước 3: Admin DUYỆT yêu cầu

```http
POST https://localhost:7001/api/v1/admin/stock-requests/{{requestId}}/review
Authorization: Bearer {{ADMIN_TOKEN}}
Content-Type: application/json

{
  "isApproved": true,
  "adminNote": "Đã duyệt yêu cầu. Staff tại trạm hãy xác nhận khi nhận đủ pin."
}
```

### ✅ Expected Response (200 OK)
```json
{
  "message": "✅ Yêu cầu đã được duyệt thành công! Hệ thống đã TỰ ĐỘNG tạo yêu cầu tăng pin (BulkCreateRequest) và gửi thông báo đến Staff tại trạm để xác nhận.",
  "requestId": "{{requestId}}",
  "status": "Approved",
  "bulkCreateRequestId": "{{bulkCreateRequestId}}",
  "adminNote": "Đã duyệt yêu cầu. Staff tại trạm hãy xác nhận khi nhận đủ pin."
}
```

**🔖 LƯU LẠI:** `bulkCreateRequestId` để dùng cho bước tiếp theo

### ❌ Test Error Cases

#### 1. Duyệt yêu cầu đã được duyệt
```http
POST https://localhost:7001/api/v1/admin/stock-requests/{{requestId}}/review
Authorization: Bearer {{ADMIN_TOKEN}}
Content-Type: application/json

{
  "isApproved": true,
  "adminNote": "Duyệt lại"
}
```

**Expected:** 400 Bad Request - "Yêu cầu này đã được xử lý rồi."

#### 2. Request không tồn tại
```http
POST https://localhost:7001/api/v1/admin/stock-requests/00000000-0000-0000-0000-000000000000/review
Authorization: Bearer {{ADMIN_TOKEN}}
Content-Type: application/json

{
  "isApproved": true,
  "adminNote": "Test"
}
```

**Expected:** 404 Not Found

---

## ✅ TEST CASE 3: STAFF XÁC NHẬN NHẬN PIN

### Bước 1: Xem BulkCreateRequest vừa được tạo tự động

```http
GET https://localhost:7001/api/bulk-create-requests/{{bulkCreateRequestId}}
Authorization: Bearer {{STAFF_TOKEN}}
```

### ✅ Expected Response (200 OK)
```json
{
  "id": "{{bulkCreateRequestId}}",
  "stationId": "{{stationId}}",
  "batteryModelId": "{{batteryModelId}}",
  "quantity": 15,
  "status": "PendingConfirmation",
  "requestedByAdminId": "{{adminUserId}}",
  "requestDate": "2025-11-02T14:35:00Z",
  "staffNotes": "Tạo tự động từ yêu cầu tăng pin #{{requestId}}"
}
```

### Bước 2: Staff XÁC NHẬN nhận pin

```http
POST https://localhost:7001/api/bulk-create-requests/{{bulkCreateRequestId}}/confirm
Authorization: Bearer {{STAFF_TOKEN}}
Content-Type: application/json

{
  "confirmedByStaffId": "{{staffUserId}}"
}
```

### ✅ Expected Response (200 OK)
```json
{
  "message": "Đã xác nhận yêu cầu tạo pin hàng loạt thành công!",
  "bulkCreateRequestId": "{{bulkCreateRequestId}}",
  "batteryUnitsCreated": 15,
  "inventoryUpdated": true
}
```

### Bước 3: Kiểm tra lại BatteryStockRequest đã Completed

```http
GET https://localhost:7001/api/v1/staff/stock-requests/{{requestId}}
Authorization: Bearer {{STAFF_TOKEN}}
```

### ✅ Expected Response (200 OK)
- `status` phải là `"Completed"` ⭐
- `relatedBulkCreateRequestId` phải có giá trị `{{bulkCreateRequestId}}`

---

## ✅ TEST CASE 4: ADMIN TỪ CHỐI YÊU CẦU

### Bước 1: Tạo yêu cầu mới (Staff)

```http
POST https://localhost:7001/api/v1/staff/stock-requests
Authorization: Bearer {{STAFF_TOKEN}}
Content-Type: application/json

{
  "stationId": "{{stationId}}",
  "batteryModelId": "{{batteryModelId}}",
  "quantity": 50,
  "staffNote": "Yêu cầu sẽ bị từ chối"
}
```

**🔖 LƯU:** `requestId2`

### Bước 2: Admin TỪ CHỐI yêu cầu

```http
POST https://localhost:7001/api/v1/admin/stock-requests/{{requestId2}}/review
Authorization: Bearer {{ADMIN_TOKEN}}
Content-Type: application/json

{
  "isApproved": false,
  "adminNote": "Ngân sách tháng này không đủ. Vui lòng gửi lại yêu cầu vào đầu tháng sau."
}
```

### ✅ Expected Response (200 OK)
```json
{
  "message": "❌ Yêu cầu đã được từ chối.",
  "requestId": "{{requestId2}}",
  "status": "Rejected",
  "adminNote": "Ngân sách tháng này không đủ. Vui lòng gửi lại yêu cầu vào đầu tháng sau."
}
```

### Bước 3: Kiểm tra request bị từ chối

```http
GET https://localhost:7001/api/v1/staff/stock-requests/{{requestId2}}
Authorization: Bearer {{STAFF_TOKEN}}
```

### ✅ Expected Response (200 OK)
- `status` = `"Rejected"`
- `adminNote` có nội dung từ chối
- `relatedBulkCreateRequestId` = `null` (KHÔNG có BulkCreateRequest được tạo)

---

## 🗄️ KIỂM TRA DATABASE

### 1. Kiểm tra BatteryStockRequests

```sql
-- Xem tất cả yêu cầu
SELECT 
    Id,
    Status,
    Quantity,
    StaffNote,
    AdminNote,
    RelatedBulkCreateRequestId,
    RequestDate,
    AdminReviewDate
FROM BatteryStockRequests
ORDER BY RequestDate DESC;

-- Kiểm tra request vừa tạo
SELECT * FROM BatteryStockRequests 
WHERE Id = '{{requestId}}';
```

### 2. Kiểm tra BulkCreateRequests tự động

```sql
-- Xem BulkCreateRequest được tạo tự động
SELECT 
    Id,
    StationId,
    BatteryModelId,
    Quantity,
    Status,
    StaffNotes,
    RequestDate
FROM BulkCreateRequests
WHERE Id = '{{bulkCreateRequestId}}';
```

### 3. Kiểm tra BatteryUnits được tạo

```sql
-- Xem pin được tạo từ BulkCreateRequest
SELECT 
    Id,
    BatteryModelId,
    CurrentStationId,
    Status,
    CreatedAt
FROM BatteryUnits
WHERE CreatedViaRequestId = '{{bulkCreateRequestId}}'
ORDER BY CreatedAt DESC;
```

### 4. Kiểm tra BatteryInventory đã tăng

```sql
-- Xem inventory trước và sau
SELECT 
    StationId,
    BatteryModelId,
    Quantity,
    LastUpdated
FROM BatteryInventories
WHERE StationId = '{{stationId}}' 
  AND BatteryModelId = '{{batteryModelId}}';
```

**✅ Expected:**
- `Quantity` đã tăng thêm 15 (số lượng trong yêu cầu)
- `LastUpdated` là thời điểm vừa confirm

---

## 🔔 KIỂM TRA NOTIFICATIONS

### 1. Xem tất cả notifications

```http
GET https://localhost:7001/api/v1/notifications
Authorization: Bearer {{TOKEN}}
```

### 2. Kiểm tra notifications theo loại

```sql
-- Notification khi Staff tạo yêu cầu → Admin
SELECT * FROM Notifications
WHERE Type = 'StockRequestCreated'
  AND UserId = '{{adminUserId}}'
ORDER BY CreatedAt DESC;

-- Notification khi Admin duyệt → Staff
SELECT * FROM Notifications
WHERE Type = 'StockRequestApproved'
  AND UserId = '{{staffUserId}}'
ORDER BY CreatedAt DESC;

-- Notification khi Admin từ chối → Staff
SELECT * FROM Notifications
WHERE Type = 'StockRequestRejected'
  AND UserId = '{{staffUserId}}'
ORDER BY CreatedAt DESC;
```

### ✅ Expected Notifications

#### Khi Staff tạo yêu cầu:
```json
{
  "type": "StockRequestCreated",
  "userId": "{{adminUserId}}",
  "title": "Yêu cầu tăng pin mới",
  "message": "Staff User 1 yêu cầu tăng 15 pin VinFast VF8 60kWh cho Trạm Gò Vấp",
  "isRead": false
}
```

#### Khi Admin duyệt:
```json
{
  "type": "StockRequestApproved",
  "userId": "{{staffUserId}}",
  "title": "Yêu cầu tăng pin đã được duyệt",
  "message": "Yêu cầu tăng 15 pin đã được Admin duyệt. Hãy xác nhận khi nhận đủ pin.",
  "isRead": false
}
```

#### Khi Admin từ chối:
```json
{
  "type": "StockRequestRejected",
  "userId": "{{staffUserId}}",
  "title": "Yêu cầu tăng pin bị từ chối",
  "message": "Yêu cầu tăng 50 pin bị từ chối: Ngân sách tháng này không đủ...",
  "isRead": false
}
```

---

## 📊 CHECKLIST KIỂM TRA HOÀN CHỈNH

### ✅ Luồng thành công (Happy Path)
- [ ] Staff tạo yêu cầu thành công (201)
- [ ] Staff xem được yêu cầu vừa tạo
- [ ] Staff xem được list yêu cầu của mình
- [ ] Admin xem được list yêu cầu pending
- [ ] Admin xem được chi tiết yêu cầu
- [ ] Admin duyệt yêu cầu thành công (200)
- [ ] **BulkCreateRequest được tạo TỰ ĐỘNG**
- [ ] Staff xác nhận BulkCreateRequest thành công
- [ ] **BatteryStockRequest status = Completed**
- [ ] BatteryUnits được tạo đúng số lượng
- [ ] BatteryInventory tăng đúng số lượng
- [ ] Tất cả notifications được gửi đúng

### ✅ Luồng từ chối
- [ ] Admin từ chối yêu cầu thành công
- [ ] Request status = Rejected
- [ ] KHÔNG có BulkCreateRequest được tạo
- [ ] Staff nhận notification từ chối

### ✅ Error Handling
- [ ] Quantity <= 0 → 400 Bad Request
- [ ] StationId không tồn tại → 400 Bad Request
- [ ] Staff không được assign tại trạm → 403 Forbidden
- [ ] Duyệt request đã được xử lý → 400 Bad Request
- [ ] Request không tồn tại → 404 Not Found

### ✅ Authorization
- [ ] Staff không thể duyệt yêu cầu (403)
- [ ] Admin không thể tạo yêu cầu với endpoint Staff
- [ ] Customer không thể truy cập bất kỳ endpoint nào

---

## 🎯 KẾT LUẬN

Nếu tất cả các test case trên đều PASS, tính năng đã hoạt động **HOÀN HẢO** và sẵn sàng cho production! 🚀

### 📝 Ghi chú quan trọng:
1. **Luồng tự động hoàn toàn**: Admin duyệt → Auto tạo BulkCreateRequest → Staff confirm → Auto complete BatteryStockRequest
2. **Không có thao tác thủ công**: Admin KHÔNG cần tạo BulkCreateRequest bằng tay
3. **Liên kết 2 chiều**: BatteryStockRequest ↔ BulkCreateRequest thông qua `RelatedBulkCreateRequestId`
4. **Notifications real-time**: Sử dụng SignalR để push notification tức thì

---

📅 **Ngày test:** 02/11/2025  
✨ **Trạng thái:** Sẵn sàng để test toàn bộ luồng
