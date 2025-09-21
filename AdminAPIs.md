# Admin APIs Documentation

## Admin Users Management APIs (`/api/v1/admin/users`)

### 1. GET /api/v1/admin/users
**Mô tả**: Lấy danh sách users với thông tin subscription

**Query Parameters**:
- `page` (int, default: 1): Trang hiện tại
- `pageSize` (int, default: 20, max: 100): Số items per trang
- `role` (Role, optional): Lọc theo vai trò (Driver, Staff, Admin)

**Response**: `PagedResult<AdminUserDto>`

### 2. GET /api/v1/admin/users/{id}
**Mô tả**: Lấy thông tin chi tiết user theo ID

### 3. POST /api/v1/admin/users
**Mô tả**: Tạo user mới
**Body**: `CreateUserRequest`

### 4. PUT /api/v1/admin/users/{id}
**Mô tả**: Cập nhật thông tin user
**Body**: `UpdateUserRequest`

### 5. DELETE /api/v1/admin/users/{id}
**Mô tả**: Xóa user (chỉ khi không có reservations/subscriptions active)

### 6. POST /api/v1/admin/users/{id}/assign-subscription
**Mô tả**: Gán gói thuê cho user
**Body**: `AssignSubscriptionRequest`

### 7. PUT /api/v1/admin/users/{id}/cancel-subscription
**Mô tả**: Hủy subscription hiện tại của user

---

## Admin Subscriptions Management APIs (`/api/v1/admin/subscriptions`)

### 1. GET /api/v1/admin/subscriptions
**Mô tả**: Lấy danh sách các gói thuê

**Query Parameters**:
- `page` (int, default: 1): Trang hiện tại
- `pageSize` (int, default: 20, max: 100): Số items per trang  
- `includeInactive` (bool, default: true): Bao gồm gói không active

### 2. GET /api/v1/admin/subscriptions/{id}
**Mô tả**: Lấy chi tiết gói thuê theo ID

### 3. POST /api/v1/admin/subscriptions
**Mô tả**: Tạo gói thuê mới
**Body**: `CreateSubscriptionPlanRequest`

### 4. PUT /api/v1/admin/subscriptions/{id}
**Mô tả**: Cập nhật gói thuê
**Body**: `UpdateSubscriptionPlanRequest`

### 5. DELETE /api/v1/admin/subscriptions/{id}
**Mô tả**: Xóa gói thuê (chỉ khi không có subscriptions active)

### 6. PUT /api/v1/admin/subscriptions/{id}/deactivate
**Mô tả**: Deactivate gói thuê

### 7. PUT /api/v1/admin/subscriptions/{id}/activate
**Mô tả**: Activate gói thuê

### 8. GET /api/v1/admin/subscriptions/user-subscriptions
**Mô tả**: Lấy danh sách subscriptions của users

**Query Parameters**:
- `page` (int, default: 1): Trang hiện tại
- `pageSize` (int, default: 20, max: 100): Số items per trang
- `status` (UserSubscriptionStatus, optional): Lọc theo trạng thái

---

## Admin Reports & Analytics APIs (`/api/v1/admin/reports`)

### 1. GET /api/v1/admin/reports/overview
**Mô tả**: Tổng quan hệ thống
**Response**: `SystemOverviewDto`
- Tổng số users, stations, batteries
- Số lượt swap hôm nay
- Doanh thu hôm nay
- Subscriptions đang active

### 2. GET /api/v1/admin/reports/daily-swaps
**Mô tả**: Báo cáo lượt swap theo ngày

**Query Parameters**:
- `from` (DateTime, optional): Từ ngày (default: 30 ngày trước)
- `to` (DateTime, optional): Đến ngày (default: hôm nay)

**Response**: `Array<DailySwapReportDto>`

### 3. GET /api/v1/admin/reports/revenue
**Mô tả**: Báo cáo doanh thu theo ngày

**Query Parameters**:
- `from` (DateTime, optional): Từ ngày (default: 30 ngày trước) 
- `to` (DateTime, optional): Đến ngày (default: hôm nay)

**Response**: `Array<RevenueReportDto>`

### 4. GET /api/v1/admin/reports/inventory
**Mô tả**: Báo cáo tồn kho batteries theo trạm
**Response**: `Array<InventoryReportDto>`

### 5. GET /api/v1/admin/reports/top-stations
**Mô tả**: Top trạm có nhiều swap nhất

**Query Parameters**:
- `limit` (int, default: 10, max: 100): Số lượng trạm top
- `from` (DateTime, optional): Từ ngày (default: 30 ngày trước)
- `to` (DateTime, optional): Đến ngày (default: hôm nay)

**Response**: `Array<TopStationDto>`

### 6. GET /api/v1/admin/reports/swap-trends
**Mô tả**: Xu hướng swap theo thời gian

**Query Parameters**:
- `from` (DateTime, optional): Từ ngày (default: 30 ngày trước)
- `to` (DateTime, optional): Đến ngày (default: hôm nay)

**Response**: `Array<SwapTrendDto>`

---

## Sample DTOs

### AdminUserDto
```json
{
  "id": "guid",
  "email": "user@example.com",
  "name": "User Name",
  "phone": "+84123456789",
  "role": "Driver", // Driver, Staff, Admin
  "createdAt": "2024-01-01T00:00:00Z",
  "lastLogin": "2024-01-01T00:00:00Z",
  "currentSubscriptionPlan": "Premium Plan",
  "subscriptionEndDate": "2024-02-01T00:00:00Z",
  "subscriptionStatus": "Active" // Active, Expired, Cancelled, Suspended
}
```

### SystemOverviewDto
```json
{
  "totalUsers": 1000,
  "activeUsers": 800,
  "totalStations": 50,
  "activeStations": 48,
  "totalBatteries": 500,
  "availableBatteries": 350,
  "todaySwaps": 120,
  "todayRevenue": 2400000.00,
  "activeSubscriptions": 600,
  "lastUpdated": "2024-01-01T12:00:00Z"
}
```

### DailySwapReportDto
```json
{
  "date": "2024-01-01",
  "totalSwaps": 150,
  "successfulSwaps": 145,
  "failedSwaps": 5,
  "totalRevenue": 3000000.00,
  "uniqueUsers": 120
}
```

## Authorization
Tất cả endpoints yêu cầu:
- Authentication (Bearer token)
- Role: Admin

## Error Responses
```json
{
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "Resource not found"
  }
}
```