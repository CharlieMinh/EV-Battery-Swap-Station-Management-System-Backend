# 📘 HƯỚNG DẪN FRONTEND - TÍNH NĂNG YÊU CẦU TĂNG PIN

> **Mục đích:** Giải thích chi tiết luồng hoạt động của tính năng yêu cầu tăng pin để Frontend có thể hiểu và tích hợp dễ dàng.

---

## 🎯 TỔNG QUAN CHỨC NĂNG

### Vấn đề giải quyết:
- Staff tại trạm cần nhiều pin hơn để phục vụ khách hàng
- Admin cần duyệt yêu cầu trước khi thực hiện tăng pin
- Cần tự động hóa quy trình để giảm thao tác thủ công

### Giải pháp:
Hệ thống cho phép Staff **yêu cầu Admin tăng pin**, Admin **duyệt yêu cầu**, sau đó hệ thống **TỰ ĐỘNG tạo BulkCreateRequest** (yêu cầu tạo pin hàng loạt). Staff chỉ cần **xác nhận khi nhận đủ pin vật lý**.

---

## 👥 CÁC VAI TRÒ VÀ QUYỀN HẠN

### 1. **Staff (Nhân viên trạm)**
- ✅ Tạo yêu cầu tăng pin cho trạm mình được phân công
- ✅ Xem danh sách yêu cầu của chính mình
- ✅ Xem chi tiết từng yêu cầu
- ✅ Xác nhận nhận pin khi BulkCreateRequest được tạo
- ❌ **KHÔNG** được tạo yêu cầu cho trạm khác
- ❌ **KHÔNG** được duyệt/từ chối yêu cầu

### 2. **Admin (Quản trị viên)**
- ✅ Xem tất cả yêu cầu chờ duyệt
- ✅ Xem chi tiết bất kỳ yêu cầu nào
- ✅ Duyệt hoặc từ chối yêu cầu
- ✅ Xem lịch sử tất cả yêu cầu
- ❌ **KHÔNG** tạo yêu cầu (chỉ Staff tạo)

### 3. **Customer (Khách hàng)**
- ❌ **KHÔNG** có quyền truy cập bất kỳ chức năng nào

---

## 🔄 LUỒNG HOẠT ĐỘNG CHI TIẾT

### **BƯỚC 1: Staff tạo yêu cầu** 📝

```
┌─────────────────────────────────────────────────┐
│  Staff nhận thấy trạm thiếu pin                 │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff điền form:                               │
│  - Chọn loại pin cần tăng                      │
│  - Nhập số lượng                                │
│  - Viết ghi chú (tùy chọn)                     │
└─────────────────────────────────────────────────┘
                      │
                      ▼
        POST /api/v1/staff/stock-requests
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Backend xử lý:                                 │
│  1. Kiểm tra Staff có assign tại trạm không    │
│  2. Validate dữ liệu                           │
│  3. Tạo BatteryStockRequest                    │
│     - Status: PendingAdminReview               │
│  4. Gửi notification đến TOÀN BỘ Admin        │
│  5. Gửi SignalR real-time notification         │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Response 201 Created:                          │
│  {                                              │
│    "message": "✅ Yêu cầu đã được gửi...",     │
│    "request": {                                 │
│      "id": "guid",                             │
│      "stationName": "Trạm Gò Vấp",            │
│      "batteryModelName": "VF8 60kWh",         │
│      "quantity": 10,                           │
│      "status": "PendingAdminReview",          │
│      ...                                       │
│    }                                           │
│  }                                             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị:                             │
│  - Toast success "Yêu cầu đã được gửi"        │
│  - Cập nhật danh sách yêu cầu của Staff       │
│  - Hiển thị status "Chờ Admin duyệt"          │
└─────────────────────────────────────────────────┘
```

---

### **BƯỚC 2: Admin nhận thông báo** 🔔

```
┌─────────────────────────────────────────────────┐
│  Admin nhận notification real-time:             │
│  - SignalR push notification                    │
│  - Database notification (bell icon)            │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Admin click vào notification                   │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend chuyển đến trang:                     │
│  "Danh sách yêu cầu tăng pin chờ duyệt"        │
└─────────────────────────────────────────────────┘
                      │
                      ▼
        GET /api/v1/admin/stock-requests/pending
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Backend trả về danh sách yêu cầu:              │
│  [                                              │
│    {                                            │
│      "id": "guid",                             │
│      "stationName": "Trạm Gò Vấp",            │
│      "batteryModelName": "VF8 60kWh",         │
│      "quantity": 10,                           │
│      "staffNote": "Cần bổ sung...",           │
│      "requestedByStaffName": "Nguyễn Văn A",  │
│      "requestDate": "2025-11-02T14:30:00Z",   │
│      "status": "PendingAdminReview"           │
│    },                                          │
│    ...                                         │
│  ]                                             │
└─────────────────────────────────────────────────┘
```

---

### **BƯỚC 3: Admin duyệt yêu cầu** ✅

```
┌─────────────────────────────────────────────────┐
│  Admin xem chi tiết yêu cầu                     │
└─────────────────────────────────────────────────┘
                      │
                      ▼
        GET /api/v1/admin/stock-requests/{id}
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị form duyệt:                  │
│  - Thông tin chi tiết yêu cầu                  │
│  - Radio button: Duyệt / Từ chối              │
│  - Textbox: Ghi chú của Admin (tùy chọn)      │
│  - Button: "Xác nhận"                          │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Admin chọn DUYỆT và click "Xác nhận"          │
└─────────────────────────────────────────────────┘
                      │
                      ▼
    POST /api/v1/admin/stock-requests/{id}/review
    Body: {
      "isApproved": true,
      "adminNote": "Đã duyệt, Staff hãy xác nhận"
    }
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Backend xử lý (QUAN TRỌNG):                    │
│  1. Cập nhật BatteryStockRequest:              │
│     - Status: PendingAdminReview → Approved    │
│     - AdminReviewerId: ID của Admin            │
│     - AdminReviewDate: Thời gian hiện tại      │
│     - AdminNote: Ghi chú của Admin             │
│                                                 │
│  2. ⭐ TỰ ĐỘNG tạo BulkCreateRequest:           │
│     - StationId: Copy từ yêu cầu Staff         │
│     - BatteryModelId: Copy từ yêu cầu Staff    │
│     - Quantity: Copy từ yêu cầu Staff          │
│     - Status: PendingConfirmation              │
│     - RequestedByAdminId: ID của Admin         │
│     - StaffNotes: Tổng hợp thông tin từ yêu cầu│
│                                                 │
│  3. Liên kết 2 yêu cầu:                        │
│     - BatteryStockRequest.RelatedBulkCreate... │
│       = BulkCreateRequest.Id                   │
│                                                 │
│  4. Gửi notification đến Staff tại trạm:       │
│     "Admin đã duyệt, hãy xác nhận khi nhận pin"│
│                                                 │
│  5. Gửi SignalR real-time notification         │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Response 200 OK:                               │
│  {                                              │
│    "message": "✅ Đã duyệt thành công!         │
│                Hệ thống đã TỰ ĐỘNG tạo        │
│                BulkCreateRequest...",          │
│    "requestId": "guid",                        │
│    "status": "Approved",                       │
│    "bulkCreateRequestId": "guid-mới",  ⭐     │
│    "adminNote": "..."                          │
│  }                                             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị:                             │
│  - Toast success                                │
│  - Badge "Đã duyệt"                            │
│  - Link đến BulkCreateRequest vừa tạo          │
│  - Cập nhật danh sách pending requests         │
└─────────────────────────────────────────────────┘
```

---

### **BƯỚC 4: Staff nhận thông báo và xác nhận** 📦

```
┌─────────────────────────────────────────────────┐
│  Staff tại trạm nhận notification:              │
│  "Admin đã duyệt yêu cầu của bạn"              │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff click notification                       │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend chuyển đến trang:                     │
│  "Chi tiết yêu cầu tăng pin"                   │
└─────────────────────────────────────────────────┘
                      │
                      ▼
        GET /api/v1/staff/stock-requests/{id}
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Response:                                      │
│  {                                              │
│    "status": "Approved",                       │
│    "adminNote": "Đã duyệt...",                │
│    "relatedBulkCreateRequestId": "guid",  ⭐   │
│    ...                                         │
│  }                                             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị:                             │
│  - Badge "Đã duyệt bởi Admin XXX"              │
│  - Ghi chú của Admin                           │
│  - Button: "Xem yêu cầu tạo pin"               │
│    (Link đến BulkCreateRequest)                │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff click "Xem yêu cầu tạo pin"             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
        GET /api/bulk-create-requests/{bulkId}
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị trang BulkCreateRequest:     │
│  - Thông tin: Trạm, Loại pin, Số lượng        │
│  - Status: PendingConfirmation                 │
│  - Button: "Xác nhận đã nhận đủ pin"          │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff nhận đủ pin vật lý và click xác nhận    │
└─────────────────────────────────────────────────┘
                      │
                      ▼
    POST /api/bulk-create-requests/{bulkId}/confirm
    Body: {
      "confirmedByStaffId": "guid"
    }
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Backend xử lý:                                 │
│  1. Cập nhật BulkCreateRequest:                │
│     - Status: PendingConfirmation → Confirmed  │
│                                                 │
│  2. Tạo BatteryUnits (số lượng = Quantity):    │
│     - Insert vào bảng BatteryUnits             │
│     - Status: Available                        │
│     - CurrentStationId: StationId của request  │
│                                                 │
│  3. Cập nhật BatteryInventory:                 │
│     - Tăng Quantity tại trạm                   │
│                                                 │
│  4. ⭐ TỰ ĐỘNG cập nhật BatteryStockRequest:    │
│     - Status: Approved → Completed             │
│     (Gọi CompleteStockRequestAsync)            │
│                                                 │
│  5. Gửi notification đến Admin:                │
│     "Staff đã xác nhận nhận pin"               │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Response 200 OK:                               │
│  {                                              │
│    "message": "Đã xác nhận thành công!",      │
│    "bulkCreateRequestId": "guid",              │
│    "batteryUnitsCreated": 10,                  │
│    "inventoryUpdated": true                    │
│  }                                             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị:                             │
│  - Toast success "Đã nhận 10 pin thành công"  │
│  - Cập nhật inventory widget (+10)             │
│  - Badge "Hoàn thành"                          │
│  - Disable button "Xác nhận"                   │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff quay lại xem BatteryStockRequest:       │
│  - Status: "Completed" ✅                      │
│  - Hiển thị toàn bộ lịch sử                    │
└─────────────────────────────────────────────────┘
```

---

### **BƯỚC 5 (Alternative): Admin từ chối** ❌

```
┌─────────────────────────────────────────────────┐
│  Admin chọn TỪ CHỐI và click "Xác nhận"        │
└─────────────────────────────────────────────────┘
                      │
                      ▼
    POST /api/v1/admin/stock-requests/{id}/review
    Body: {
      "isApproved": false,
      "adminNote": "Ngân sách không đủ tháng này"
    }
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Backend xử lý:                                 │
│  1. Cập nhật BatteryStockRequest:              │
│     - Status: PendingAdminReview → Rejected    │
│     - AdminNote: Ghi chú của Admin             │
│                                                 │
│  2. ⚠️ KHÔNG tạo BulkCreateRequest              │
│                                                 │
│  3. Gửi notification đến Staff đã yêu cầu:     │
│     "Yêu cầu bị từ chối: [Lý do]"             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Response 200 OK:                               │
│  {                                              │
│    "message": "❌ Yêu cầu đã được từ chối.",   │
│    "requestId": "guid",                        │
│    "status": "Rejected",                       │
│    "adminNote": "Ngân sách không đủ..."       │
│  }                                             │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Frontend hiển thị:                             │
│  - Toast warning                                │
│  - Badge "Đã từ chối"                          │
│  - Hiển thị lý do từ chối                      │
│  - Không có link đến BulkCreateRequest         │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff nhận notification:                       │
│  "Yêu cầu tăng pin của bạn bị từ chối"        │
└─────────────────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│  Staff có thể:                                  │
│  - Tạo yêu cầu mới với điều chỉnh              │
│  - Liên hệ Admin để thảo luận                  │
└─────────────────────────────────────────────────┘
```

---

## 📊 SƠ ĐỒ TRẠNG THÁI (STATE DIAGRAM)

```
BatteryStockRequest Status Flow:
═══════════════════════════════════════════════════

    ┌──────────────────┐
    │   Staff tạo      │
    │   yêu cầu        │
    └────────┬─────────┘
             │
             ▼
    ┌────────────────────┐
    │ PendingAdminReview │ ◄─── Trạng thái khởi tạo
    └────────┬───────────┘
             │
      ┌──────┴───────┐
      │              │
      ▼              ▼
┌──────────┐   ┌──────────┐
│ Approved │   │ Rejected │
└─────┬────┘   └──────────┘
      │              │
      │              └────► END (Kết thúc)
      │
      │ (Staff xác nhận BulkCreateRequest)
      │
      ▼
┌──────────┐
│Completed │ ◄─── Trạng thái cuối cùng thành công
└──────────┘
      │
      └────► END
```

---

## 📡 API ENDPOINTS - TỔNG HỢP

### **Staff APIs**

#### 1. Tạo yêu cầu tăng pin
```http
POST /api/v1/staff/stock-requests
Authorization: Bearer {staff_token}
Content-Type: application/json

Request Body:
{
  "stationId": "guid",
  "batteryModelId": "guid",
  "quantity": 10,
  "staffNote": "Cần bổ sung pin cho tuần sau"
}

Response 201:
{
  "message": "✅ Yêu cầu tăng pin đã được gửi đến Admin.",
  "request": {
    "id": "guid",
    "stationId": "guid",
    "stationName": "Trạm Gò Vấp",
    "batteryModelId": "guid",
    "batteryModelName": "VinFast VF8 60kWh",
    "quantity": 10,
    "staffNote": "Cần bổ sung pin cho tuần sau",
    "status": "PendingAdminReview",
    "requestedByStaffId": "guid",
    "requestedByStaffName": "Nguyễn Văn A",
    "requestDate": "2025-11-02T14:30:00Z",
    "updatedAt": "2025-11-02T14:30:00Z"
  }
}
```

#### 2. Xem chi tiết yêu cầu
```http
GET /api/v1/staff/stock-requests/{id}
Authorization: Bearer {staff_token}

Response 200:
{
  "id": "guid",
  "stationName": "Trạm Gò Vấp",
  "batteryModelName": "VinFast VF8 60kWh",
  "quantity": 10,
  "staffNote": "...",
  "status": "Approved",
  "requestedByStaffName": "Nguyễn Văn A",
  "requestDate": "2025-11-02T14:30:00Z",
  "adminReviewerName": "Admin User",
  "adminReviewDate": "2025-11-02T15:00:00Z",
  "adminNote": "Đã duyệt",
  "relatedBulkCreateRequestId": "guid-bulk-request",  ⭐
  "updatedAt": "2025-11-02T15:00:00Z"
}
```

#### 3. Xem tất cả yêu cầu của mình
```http
GET /api/v1/staff/stock-requests/mine
Authorization: Bearer {staff_token}

Response 200:
[
  {
    "id": "guid",
    "stationName": "Trạm Gò Vấp",
    "batteryModelName": "VinFast VF8 60kWh",
    "quantity": 10,
    "status": "PendingAdminReview",
    "requestDate": "2025-11-02T14:30:00Z",
    ...
  },
  ...
]
```

---

### **Admin APIs**

#### 1. Xem danh sách yêu cầu chờ duyệt
```http
GET /api/v1/admin/stock-requests/pending
Authorization: Bearer {admin_token}

Response 200:
[
  {
    "id": "guid",
    "stationName": "Trạm Gò Vấp",
    "batteryModelName": "VinFast VF8 60kWh",
    "quantity": 10,
    "staffNote": "Cần bổ sung...",
    "status": "PendingAdminReview",
    "requestedByStaffName": "Nguyễn Văn A",
    "requestDate": "2025-11-02T14:30:00Z"
  },
  ...
]
```

#### 2. Duyệt hoặc từ chối yêu cầu
```http
POST /api/v1/admin/stock-requests/{id}/review
Authorization: Bearer {admin_token}
Content-Type: application/json

Request Body (Duyệt):
{
  "isApproved": true,
  "adminNote": "Đã duyệt, Staff hãy xác nhận khi nhận pin"
}

Response 200 (Approved):
{
  "message": "✅ Yêu cầu đã được duyệt thành công! Hệ thống đã TỰ ĐỘNG tạo yêu cầu tăng pin (BulkCreateRequest)...",
  "requestId": "guid",
  "status": "Approved",
  "bulkCreateRequestId": "guid-bulk-request",  ⭐ QUAN TRỌNG
  "adminNote": "Đã duyệt..."
}

Request Body (Từ chối):
{
  "isApproved": false,
  "adminNote": "Ngân sách không đủ tháng này"
}

Response 200 (Rejected):
{
  "message": "❌ Yêu cầu đã được từ chối.",
  "requestId": "guid",
  "status": "Rejected",
  "adminNote": "Ngân sách không đủ..."
}
```

#### 3. Xem chi tiết yêu cầu
```http
GET /api/v1/admin/stock-requests/{id}
Authorization: Bearer {admin_token}

Response 200: (Giống Staff API nhưng Admin có thể xem tất cả)
```

---

### **Existing BulkCreateRequest API** (Đã có sẵn)

#### Xác nhận nhận pin (Staff)
```http
POST /api/bulk-create-requests/{bulkCreateRequestId}/confirm
Authorization: Bearer {staff_token}
Content-Type: application/json

Request Body:
{
  "confirmedByStaffId": "guid"
}

Response 200:
{
  "message": "Đã xác nhận yêu cầu tạo pin hàng loạt thành công!",
  "bulkCreateRequestId": "guid",
  "batteryUnitsCreated": 10,
  "inventoryUpdated": true
}

Chú ý:
- Endpoint này đã tồn tại từ trước
- Sau khi confirm, hệ thống TỰ ĐỘNG:
  1. Tạo 10 BatteryUnits mới
  2. Cập nhật BatteryInventory (+10)
  3. Cập nhật BatteryStockRequest.Status = Completed ⭐
```

---

## 🔔 SIGNALR NOTIFICATIONS

### Events cần subscribe:

#### 1. **Admin listen:**
```javascript
// Khi có yêu cầu tăng pin mới
connection.on("NewStockRequest", (request) => {
  // Hiển thị notification popup
  // Badge số lượng pending requests tăng
  // Có thể play sound notification
  showNotification({
    title: "Yêu cầu tăng pin mới",
    message: `${request.requestedByStaffName} yêu cầu tăng ${request.quantity} pin`,
    type: "info",
    action: () => navigateTo(`/admin/stock-requests/${request.id}`)
  });
});
```

#### 2. **Staff listen:**
```javascript
// Khi yêu cầu được duyệt
connection.on("StockRequestApproved", (data) => {
  showNotification({
    title: "Yêu cầu đã được duyệt ✅",
    message: `Admin đã duyệt yêu cầu tăng ${data.quantity} pin`,
    type: "success",
    action: () => navigateTo(`/staff/stock-requests/${data.requestId}`)
  });
});

// Khi yêu cầu bị từ chối
connection.on("StockRequestRejected", (data) => {
  showNotification({
    title: "Yêu cầu bị từ chối ❌",
    message: data.adminNote,
    type: "warning",
    action: () => navigateTo(`/staff/stock-requests/${data.requestId}`)
  });
});

// Khi có BulkCreateRequest mới (đã có sẵn từ trước)
connection.on("NewBulkRequest", (bulkRequest) => {
  showNotification({
    title: "Yêu cầu tạo pin mới",
    message: `Có ${bulkRequest.quantity} pin cần xác nhận`,
    type: "info",
    action: () => navigateTo(`/bulk-create-requests/${bulkRequest.id}`)
  });
});
```

---

## 🎨 UI/UX GỢI Ý

### **Staff Dashboard**

#### Widget: "Yêu cầu tăng pin của tôi"
```
┌─────────────────────────────────────────────────┐
│ 📋 Yêu cầu tăng pin của tôi                     │
├─────────────────────────────────────────────────┤
│                                                 │
│ 🟡 Chờ duyệt: 2 yêu cầu                        │
│ ✅ Đã duyệt: 5 yêu cầu (3 đang chờ xác nhận)   │
│ ❌ Từ chối: 1 yêu cầu                          │
│ ✓ Hoàn thành: 12 yêu cầu                       │
│                                                 │
│ [+ Tạo yêu cầu mới]  [Xem tất cả]              │
└─────────────────────────────────────────────────┘
```

#### Form tạo yêu cầu:
```
┌─────────────────────────────────────────────────┐
│ Tạo yêu cầu tăng pin                            │
├─────────────────────────────────────────────────┤
│                                                 │
│ Trạm: Trạm Gò Vấp (không thể thay đổi)        │
│                                                 │
│ Loại pin: [Dropdown - BatteryModels]           │
│   ├─ VinFast VF8 60kWh                         │
│   ├─ VinFast VF9 90kWh                         │
│   └─ ...                                       │
│                                                 │
│ Số lượng: [____10____] pin                    │
│   (Tối thiểu: 1)                               │
│                                                 │
│ Ghi chú (tùy chọn):                            │
│ ┌───────────────────────────────────────────┐  │
│ │ Tuần sau dự kiến có nhiều khách hàng      │  │
│ │ đăng ký mới, cần bổ sung thêm pin        │  │
│ └───────────────────────────────────────────┘  │
│                                                 │
│ [Hủy]  [Gửi yêu cầu]                           │
└─────────────────────────────────────────────────┘
```

#### Chi tiết yêu cầu:
```
┌─────────────────────────────────────────────────┐
│ Chi tiết yêu cầu #12345                         │
├─────────────────────────────────────────────────┤
│                                                 │
│ Trạng thái: 🟢 Đã duyệt                        │
│ Trạm: Trạm Gò Vấp                              │
│ Loại pin: VinFast VF8 60kWh                    │
│ Số lượng: 10 pin                               │
│ Ghi chú: Cần bổ sung...                        │
│                                                 │
│ ──────────────────────────────────────────────  │
│ Người yêu cầu: Nguyễn Văn A (Bạn)             │
│ Ngày yêu cầu: 02/11/2025 14:30                 │
│                                                 │
│ ──────────────────────────────────────────────  │
│ ✅ Đã duyệt bởi: Admin User                    │
│ Ngày duyệt: 02/11/2025 15:00                   │
│ Ghi chú Admin: "Đã duyệt, Staff hãy xác nhận   │
│                 khi nhận đủ pin"               │
│                                                 │
│ 📦 Yêu cầu tạo pin liên quan:                  │
│ [→ Xem chi tiết BulkCreateRequest #67890]      │
│                                                 │
│ [← Quay lại]                                   │
└─────────────────────────────────────────────────┘
```

---

### **Admin Dashboard**

#### Widget: "Yêu cầu tăng pin chờ duyệt"
```
┌─────────────────────────────────────────────────┐
│ ⚠️ Yêu cầu tăng pin chờ duyệt (3)               │
├─────────────────────────────────────────────────┤
│                                                 │
│ 1. Trạm Gò Vấp - VF8 60kWh - 10 pin           │
│    Người yêu cầu: Nguyễn Văn A                 │
│    Ngày: 02/11/2025 14:30                      │
│    [Xem]                                       │
│                                                 │
│ 2. Trạm Thủ Đức - VF9 90kWh - 5 pin           │
│    Người yêu cầu: Trần Thị B                   │
│    Ngày: 02/11/2025 13:15                      │
│    [Xem]                                       │
│                                                 │
│ 3. ...                                         │
│                                                 │
│ [Xem tất cả →]                                 │
└─────────────────────────────────────────────────┘
```

#### Form duyệt yêu cầu:
```
┌─────────────────────────────────────────────────┐
│ Duyệt yêu cầu tăng pin #12345                   │
├─────────────────────────────────────────────────┤
│                                                 │
│ Trạm: Trạm Gò Vấp                              │
│ Loại pin: VinFast VF8 60kWh                    │
│ Số lượng: 10 pin                               │
│ Người yêu cầu: Nguyễn Văn A                    │
│ Ngày yêu cầu: 02/11/2025 14:30                 │
│                                                 │
│ Ghi chú Staff:                                  │
│ "Tuần sau dự kiến có nhiều khách hàng..."     │
│                                                 │
│ ──────────────────────────────────────────────  │
│                                                 │
│ Quyết định: ⚪ Duyệt  ⚪ Từ chối                │
│                                                 │
│ Ghi chú của bạn (tùy chọn):                    │
│ ┌───────────────────────────────────────────┐  │
│ │ Đã duyệt. Staff tại trạm hãy xác nhận     │  │
│ │ khi nhận đủ pin vật lý.                   │  │
│ └───────────────────────────────────────────┘  │
│                                                 │
│ [Hủy]  [Xác nhận]                              │
└─────────────────────────────────────────────────┘
```

---

## ⚠️ ERROR HANDLING

### Các lỗi thường gặp và cách xử lý:

#### 1. **Staff không được phân công tại trạm**
```json
Response 403 Forbidden:
{
  "error": {
    "code": "FORBIDDEN",
    "message": "Bạn không có quyền tạo yêu cầu cho trạm này."
  }
}

Frontend:
- Toast error
- Disable form nếu Staff không có StationId
```

#### 2. **Số lượng không hợp lệ**
```json
Response 400 Bad Request:
{
  "error": {
    "code": "INVALID_QUANTITY",
    "message": "Số lượng pin phải lớn hơn 0."
  }
}

Frontend:
- Hiển thị validation message dưới input
- Min value = 1
```

#### 3. **Duyệt yêu cầu đã được xử lý**
```json
Response 400 Bad Request:
{
  "error": {
    "code": "ALREADY_PROCESSED",
    "message": "Yêu cầu này đã được xử lý rồi."
  }
}

Frontend:
- Toast error
- Refresh data và hiển thị trạng thái hiện tại
- Disable form duyệt
```

#### 4. **StationId/BatteryModelId không tồn tại**
```json
Response 400 Bad Request:
{
  "error": {
    "code": "NOT_FOUND",
    "message": "Trạm không tồn tại." // hoặc "Loại pin không tồn tại."
  }
}

Frontend:
- Toast error
- Validate dropdown trước khi submit
```

---

## 🔐 AUTHORIZATION MATRIX

| Endpoint                                  | Staff | Admin | Customer |
|-------------------------------------------|-------|-------|----------|
| POST /staff/stock-requests                | ✅    | ❌    | ❌       |
| GET /staff/stock-requests/{id}            | ✅*   | ❌    | ❌       |
| GET /staff/stock-requests/mine            | ✅    | ❌    | ❌       |
| GET /admin/stock-requests/pending         | ❌    | ✅    | ❌       |
| GET /admin/stock-requests/{id}            | ❌    | ✅    | ❌       |
| POST /admin/stock-requests/{id}/review    | ❌    | ✅    | ❌       |
| POST /bulk-create-requests/{id}/confirm   | ✅*   | ❌    | ❌       |

*Staff chỉ được xem/xác nhận yêu cầu của chính mình hoặc tại trạm được phân công

---

## 📝 CHECKLIST TÍCH HỢP FRONTEND

### **Phase 1: Staff Features**
- [ ] Tạo page: "Danh sách yêu cầu tăng pin của tôi"
- [ ] Tạo form: "Tạo yêu cầu tăng pin mới"
- [ ] Tạo page: "Chi tiết yêu cầu tăng pin"
- [ ] Hiển thị badge trạng thái (PendingAdminReview, Approved, Rejected, Completed)
- [ ] Link từ BatteryStockRequest → BulkCreateRequest (nếu có)
- [ ] Widget dashboard hiển thị tổng quan yêu cầu

### **Phase 2: Admin Features**
- [ ] Tạo page: "Danh sách yêu cầu chờ duyệt"
- [ ] Tạo form: "Duyệt/Từ chối yêu cầu"
- [ ] Tạo page: "Chi tiết yêu cầu" (Admin view)
- [ ] Widget dashboard: "Yêu cầu chờ duyệt" với counter
- [ ] Hiển thị link đến BulkCreateRequest sau khi duyệt

### **Phase 3: Notifications**
- [ ] Subscribe SignalR events:
  - [ ] NewStockRequest (Admin)
  - [ ] StockRequestApproved (Staff)
  - [ ] StockRequestRejected (Staff)
- [ ] Bell icon notification center
- [ ] Toast notifications
- [ ] Badge counter cho pending requests

### **Phase 4: Integration**
- [ ] Link từ notification → chi tiết yêu cầu
- [ ] Link từ BatteryStockRequest → BulkCreateRequest
- [ ] Link từ BulkCreateRequest → BatteryStockRequest (reverse)
- [ ] Cập nhật inventory widget sau khi confirm
- [ ] Hiển thị lịch sử đầy đủ trong chi tiết

### **Phase 5: Error Handling & Validation**
- [ ] Form validation (quantity > 0)
- [ ] Check Staff có StationId trước khi hiển thị form
- [ ] Handle 400, 403, 404 errors
- [ ] Loading states cho tất cả API calls
- [ ] Retry logic cho failed requests

---

## 🎯 TÓM TẮT CHO FRONTEND DEVELOPER

### **3 Điểm Quan Trọng Nhất:**

1. **Tự động hóa hoàn toàn:**
   - Admin chỉ cần click "Duyệt" → Hệ thống TỰ ĐỘNG tạo BulkCreateRequest
   - Staff confirm BulkCreateRequest → Hệ thống TỰ ĐỘNG complete BatteryStockRequest
   - Frontend CHỈ cần gọi API, KHÔNG cần logic phức tạp

2. **Liên kết 2 chiều:**
   - `BatteryStockRequest.relatedBulkCreateRequestId` → Link đến BulkCreateRequest
   - Hiển thị cả 2 trong UI để user tracking được toàn bộ luồng

3. **Real-time notifications:**
   - Subscribe SignalR events để cập nhật UI tức thì
   - Không cần polling API, tiết kiệm bandwidth

### **Luồng đơn giản hóa:**
```
Staff tạo request → Admin duyệt → Staff confirm bulk request → XONG!
```

---

📅 **Ngày tạo:** 02/11/2025  
✨ **Phiên bản:** 1.0  
📧 **Liên hệ Backend:** Nếu có câu hỏi, hãy hỏi team Backend
