# 📚 API Documentation - EV Battery Swap Station Management System

> **Hệ thống quản lý trạm đổi pin xe điện**  
> Version: 1.0  
> Last Updated: November 3, 2025  
> **Tổng số API: 120 endpoints**

---

## 📑 Mục lục

1. [Authentication & Authorization](#1-authentication--authorization)
2. [Admin APIs](#2-admin-apis)
3. [Staff APIs](#3-staff-apis)
4. [Driver/Customer APIs](#4-drivercustomer-apis)
5. [Public APIs](#5-public-apis)
6. [Payment & Subscription](#6-payment--subscription)
7. [Swap Transactions](#7-swap-transactions)
8. [Reservations](#8-reservations)
9. [Battery & Inventory](#9-battery--inventory)
10. [User Management](#10-user-management)
11. [Utility APIs](#11-utility-apis)

---

## 1. Authentication & Authorization

### Auth Controller

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Auth** | `POST` | `/api/v1/Auth/register` | Đăng ký tài khoản Driver mới | Công khai |
| **Auth** | `POST` | `/api/v1/Auth/login` | Đăng nhập bằng email/password, nhận JWT token | Công khai |
| **Auth** | `POST` | `/api/v1/Auth/google-login` | Đăng nhập bằng Google ID Token | Công khai |
| **Auth** | `POST` | `/api/v1/Auth/logout` | Đăng xuất, xóa JWT cookie | Authorize |
| **Auth** | `GET` | `/api/v1/Auth/me` | Lấy thông tin profile user đang đăng nhập | Authorize |
| **Auth** | `POST` | `/api/v1/Auth/forgot-password` | Gửi mã OTP qua email để reset password | Công khai |
| **Auth** | `POST` | `/api/v1/Auth/verify-otp` | Xác thực mã OTP từ email | Công khai |
| **Auth** | `POST` | `/api/v1/Auth/reset-password` | Đặt lại mật khẩu mới với OTP đã xác thực | Công khai |

---

## 2. Admin APIs

### 2.1. Admin Battery Stock Requests

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **AdminBatteryStockRequests** | `POST` | `/api/v1/admin/stock-requests/{id}/review` | Admin duyệt/từ chối yêu cầu tăng pin từ Staff. Nếu duyệt, tự động tạo BulkCreateRequest | Admin |
| **AdminBatteryStockRequests** | `GET` | `/api/v1/admin/stock-requests/pending` | Xem tất cả yêu cầu tăng pin đang chờ duyệt | Admin |
| **AdminBatteryStockRequests** | `GET` | `/api/v1/admin/stock-requests/{id}` | Xem chi tiết một yêu cầu tăng pin | Admin |

### 2.2. Admin Stations Management

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **AdminStations** | `POST` | `/api/v1/admin/stations` | Tạo trạm đổi pin mới | Admin |
| **AdminStations** | `PUT` | `/api/v1/admin/stations/{id}` | Cập nhật thông tin trạm (địa chỉ, giờ hoạt động, v.v.) | Admin |
| **AdminStations** | `DELETE` | `/api/v1/admin/stations/{id}` | Xóa trạm (soft delete nếu có pin, hard delete nếu không) | Admin |

---

## 3. Staff APIs

### 3.1. Staff Battery Stock Requests

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **StaffBatteryStockRequests** | `POST` | `/api/v1/staff/stock-requests` | Staff tạo yêu cầu tăng pin tại trạm | Staff |
| **StaffBatteryStockRequests** | `GET` | `/api/v1/staff/stock-requests/{id}` | Xem chi tiết yêu cầu của mình | Staff |
| **StaffBatteryStockRequests** | `GET` | `/api/v1/staff/stock-requests/mine` | Xem tất cả yêu cầu tăng pin mà mình đã tạo | Staff |

### 3.2. Bulk Create Requests

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **BulkCreateRequests** | `POST` | `/api/bulk-create-requests/request` | Tạo yêu cầu thêm pin hàng loạt | Admin, Staff |
| **BulkCreateRequests** | `GET` | `/api/bulk-create-requests` | Xem danh sách bulk requests | Admin, Staff |
| **BulkCreateRequests** | `GET` | `/api/bulk-create-requests/pending` | Xem các bulk requests chờ xác nhận | Staff |
| **BulkCreateRequests** | `POST` | `/api/bulk-create-requests/{id}/confirm` | Staff xác nhận đã nhận pin và thêm vào hệ thống | Staff |
| **BulkCreateRequests** | `POST` | `/api/bulk-create-requests/{id}/reject` | Từ chối bulk request | Staff |

### 3.3. Battery Complaints Management

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **BatteryComplaints** | `GET` | `/api/BatteryComplaints` | Xem danh sách khiếu nại pin | Admin, Staff |
| **BatteryComplaints** | `GET` | `/api/BatteryComplaints/{id}` | Xem chi tiết một khiếu nại | Admin, Staff |
| **BatteryComplaints** | `POST` | `/api/BatteryComplaints/{id}/resolve` | Giải quyết khiếu nại (Reswap, NoAction, Refund) | Admin, Staff |
| **BatteryComplaints** | `POST` | `/api/BatteryComplaints/{id}/finalize-reswap` | Hoàn tất đổi pin miễn phí (Re-swap) cho khiếu nại | Admin, Staff |
| **BatteryComplaints** | `POST` | `/api/BatteryComplaints/{id}/investigate` | Chuyển khiếu nại sang trạng thái điều tra | Admin, Staff |

---

## 4. Driver/Customer APIs

### 4.1. Driver Battery Complaints

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **DriverBatteryComplaints** | `POST` | `/api/driver/complaints/report` | Driver báo cáo pin vừa nhận bị lỗi | Driver |
| **DriverBatteryComplaints** | `GET` | `/api/driver/complaints` | Xem danh sách khiếu nại của mình | Driver |
| **DriverBatteryComplaints** | `POST` | `/api/driver/complaints/{complaintId}/schedule-inspection` | Đặt lịch để Staff kiểm tra pin bị khiếu nại | Driver |
| **DriverBatteryComplaints** | `GET` | `/api/driver/complaints/{id}` | Xem chi tiết một khiếu nại | Driver |

### 4.2. Vehicles Management

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Vehicles** | `GET` | `/api/v1/Vehicles` | Xem danh sách xe đã đăng ký | Driver |
| **Vehicles** | `POST` | `/api/v1/Vehicles` | Đăng ký xe mới | Driver |
| **Vehicles** | `GET` | `/api/v1/Vehicles/{id}` | Xem chi tiết một xe | Driver |
| **Vehicles** | `PUT` | `/api/v1/Vehicles/{id}` | Cập nhật thông tin xe | Driver |
| **Vehicles** | `DELETE` | `/api/v1/Vehicles/{id}` | Xóa xe khỏi danh sách | Driver |
| **Vehicles** | `POST` | `/api/v1/Vehicles/with-url` | Đăng ký xe với ảnh URL | Driver |
| **Vehicles** | `POST` | `/api/v1/Vehicles/scan-registration` | Upload ảnh đăng ký xe, OCR tự động điền thông tin | Driver |
| **Vehicles** | `POST` | `/api/v1/Vehicles/scan-registration-url` | Quét đăng ký xe từ URL | Driver |

---

## 5. Public APIs

### 5.1. Stations

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Stations** | `GET` | `/api/v1/Stations` | Lấy danh sách trạm (có phân trang, filter theo city) | Công khai |
| **Stations** | `GET` | `/api/v1/Stations/{id}` | Xem chi tiết một trạm | Công khai |
| **Stations** | `GET` | `/api/v1/Stations/nearby` | Tìm trạm gần vị trí hiện tại (theo lat/lng, radius) | Công khai |
| **Stations** | `GET` | `/api/v1/Stations/{id}/availability` | Xem tình trạng pin tại trạm (Full/Charging/Maintenance) | Công khai |
| **Stations** | `GET` | `/api/v1/Stations/{id}/batteries` | Xem danh sách pin tại trạm (filter theo status) | Công khai |
| **Stations** | `GET` | `/api/v1/Stations/{stationId}/battery-stats` | Thống kê pin trong trạm | Công khai |

### 5.2. Battery Models

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **BatteryModels** | `GET` | `/api/BatteryModels` | Lấy danh sách loại pin (model) trong hệ thống | Authorize |
| **BatteryModels** | `GET` | `/api/BatteryModels/{id}/swap-price` | Xem giá pay-per-swap cho loại pin này | Authorize |

### 5.3. Vehicle Models

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **VehicleModels** | `GET` | `/api/v1/vehicle-models` | Lấy danh sách loại xe hỗ trợ | Công khai |
| **VehicleModels** | `GET` | `/api/v1/vehicle-models/{id}` | Xem chi tiết một loại xe | Công khai |

---

## 6. Payment & Subscription

### 6.1. Payments

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Payments** | `POST` | `/api/v1/payments/vnpay/create` | Tạo link thanh toán VNPay cho subscription | Authorize |
| **Payments** | `GET` | `/api/v1/payments/vnpay/callback` | Webhook callback từ VNPay (IPN) | Công khai (VNPay) |
| **Payments** | `GET` | `/api/v1/payments/vnpay/return` | User được redirect về từ VNPay sau thanh toán | Công khai |
| **Payments** | `POST` | `/api/v1/payments/create-pay-per-swap-reservation` | Tạo đặt lịch lẻ (Pay-per-Swap) với VNPay/Cash | Authorize |
| **Payments** | `POST` | `/api/v1/payments/{paymentId}/select-cash` | Driver chọn thanh toán bằng tiền mặt | Driver |
| **Payments** | `GET` | `/api/v1/payments` | Xem danh sách payments (Admin/Staff dashboard) | Admin, Staff |
| **Payments** | `GET` | `/api/v1/payments/my-payments` | Driver xem lịch sử thanh toán của mình | Driver |
| **Payments** | `GET` | `/api/v1/payments/{paymentId}` | Xem chi tiết một payment | Admin, Staff |
| **Payments** | `GET` | `/api/v1/payments/pending-cash` |  Staff xem danh sách thanh toán tiền mặt đang chờ xác nhận (Status=Pending, Method=Cash). Response bao gồm đầy đủ thông tin: người thanh toán, gói dịch vụ, xe, lịch hẹn, trạm | Staff, Admin |
| **Payments** | `POST` | `/api/v1/payments/{paymentId}/complete-cash` | Staff xác nhận đã nhận tiền mặt. Response bao gồm đầy đủ thông tin: người thanh toán, gói dịch vụ, xe, staff xử lý, trạm | Staff, Admin |

### 6.2. Subscriptions

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Subscriptions** | `POST` | `/api/v1/subscriptions/create-pending` | Tạo subscription chờ thanh toán + VNPay URL | Authorize |
| **Subscriptions** | `POST` | `/api/v1/subscriptions` | Tạo subscription trực tiếp (Admin use case) | Authorize |
| **Subscriptions** | `GET` | `/api/v1/subscriptions/mine` | Xem subscription hiện tại đang active | Authorize |
| **Subscriptions** | `GET` | `/api/v1/subscriptions/mine/all` | Xem tất cả subscriptions (active + expired + cancelled) | Authorize |
| **Subscriptions** | `PUT` | `/api/v1/subscriptions/mine/cancel` | Hủy subscription hiện tại | Authorize |
| **Subscriptions** | `GET` | `/api/v1/subscriptions/mine/usage` | Xem thống kê sử dụng pin trong kỳ | Authorize |

### 6.3. Subscription Plans

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **SubscriptionPlans** | `GET` | `/api/v1/subscription-plans` | Lấy danh sách gói subscription | Công khai |
| **SubscriptionPlans** | `POST` | `/api/v1/subscription-plans` | Tạo gói subscription mới | Admin |
| **SubscriptionPlans** | `GET` | `/api/v1/subscription-plans/{id}` | Xem chi tiết một gói | Công khai |
| **SubscriptionPlans** | `PUT` | `/api/v1/subscription-plans/{id}` | Cập nhật gói subscription | Admin |
| **SubscriptionPlans** | `DELETE` | `/api/v1/subscription-plans/{id}` | Xóa gói (soft delete) | Admin |

---

## 7. Swap Transactions

### Swap Transactions Management

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **SwapTransactions** | `POST` | `/api/v1/swaps/finalize-from-reservation` | Staff hoàn tất giao dịch đổi pin từ reservation đã check-in | Staff, Admin |
| **SwapTransactions** | `PUT` | `/api/v1/swaps/{id}/receive-battery` | Staff xác nhận đã nhận pin cũ từ Driver | Staff, Admin |
| **SwapTransactions** | `PUT` | `/api/v1/swaps/{id}/complete` | Driver xác nhận đã nhận pin mới và hoàn tất | Driver |
| **SwapTransactions** | `GET` | `/api/v1/swaps/history` | Xem lịch sử đổi pin của mình (có phân trang) | Driver |
| **SwapTransactions** | `GET` | `/api/v1/swaps/{id}` | Xem chi tiết một giao dịch đổi pin | Driver |
| **SwapTransactions** | `GET` | `/api/v1/swaps/current` | Xem giao dịch đang diễn ra (CheckedIn) | Driver |
| **SwapTransactions** | `GET` | `/api/v1/swaps/statistics` | Thống kê chi tiết lịch sử đổi pin | Driver |
| **SwapTransactions** | `PUT` | `/api/v1/swaps/{id}/rate` | Đánh giá và phản hồi về giao dịch | Driver |
| **SwapTransactions** | `POST` | `/api/v1/swaps/report-faulty` | Báo cáo pin vừa nhận bị lỗi (tạo complaint) | Driver |

---

## 8. Reservations

### 8.1. Slot Reservations

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **SlotReservations** | `GET` | `/api/v1/slot-reservations/available-slots` | Xem các slot còn trống trong ngày tại trạm | Authorize |
| **SlotReservations** | `GET` | `/api/v1/slot-reservations/inspection-slots` | Xem slot để đặt lịch kiểm tra pin (cho complaint) | Driver |
| **SlotReservations** | `GET` | `/api/v1/slot-reservations` | ⭐ **CẬP NHẬT**: Xem tất cả reservations (bao gồm thông tin xe: VehicleId, VehicleName, LicensePlate) | Admin, Staff |
| **SlotReservations** | `POST` | `/api/v1/slot-reservations` | Tạo reservation (với/không payment) | Authorize |
| **SlotReservations** | `GET` | `/api/v1/slot-reservations/mine` | ⭐ **CẬP NHẬT**: Xem reservations của mình (bao gồm thông tin xe) | Authorize |
| **SlotReservations** | `GET` | `/api/v1/slot-reservations/{id}` | ⭐ **CẬP NHẬT**: Xem chi tiết reservation (bao gồm thông tin xe) | Authorize |
| **SlotReservations** | `DELETE` | `/api/v1/slot-reservations/{id}` | Hủy reservation | Authorize |
| **SlotReservations** | `POST` | `/api/v1/slot-reservations/{id}/check-in` | Staff check-in Driver bằng QR Code | Staff, Admin |

**⭐ CẬP NHẬT MỚI (2025-11-07):**
- SlotReservationResponse đã thêm các trường:
  - `VehicleId` (Guid?): ID của xe
  - `VehicleName` (string?): Tên loại xe (VF3, VF5, VF8, VF9, v.v.)
  - `LicensePlate` (string?): Biển số xe
- Các trường mới này hiển thị trong response của: GET `/api/v1/slot-reservations`, GET `/api/v1/slot-reservations/mine`, GET `/api/v1/slot-reservations/{id}`

### 8.2. Reservations (Legacy)

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Reservations** | `GET` | `/api/v1/Reservations` | Xem tất cả reservations (legacy endpoint) | Admin, Staff |

---

## 9. Battery & Inventory

### 9.1. Battery Units

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **BatteryUnits** | `GET` | `/api/BatteryUnits` | Lấy danh sách tất cả pin trong hệ thống | Admin, Staff |
| **BatteryUnits** | `POST` | `/api/BatteryUnits` | Tạo pin mới | Admin, Staff |
| **BatteryUnits** | `GET` | `/api/BatteryUnits/station/{stationId}` | Xem pin tại một trạm cụ thể | Admin, Staff |
| **BatteryUnits** | `POST` | `/api/BatteryUnits/add-to-station` | Thêm pin vào trạm | Admin, Staff |
| **BatteryUnits** | `GET` | `/api/BatteryUnits/{id}` | Xem chi tiết một pin | Admin, Staff |
| **BatteryUnits** | `DELETE` | `/api/BatteryUnits/{id}` | Xóa pin | Admin |
| **BatteryUnits** | `PATCH` | `/api/BatteryUnits/{id}/status` | Cập nhật trạng thái pin (Full/Charging/Maintenance/Issued) | Admin, Staff |

### 9.2. Inventory

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Inventory** | `POST` | `/api/Inventory/add-stock` | Thêm pin vào kho | Admin, Staff |
| **Inventory** | `POST` | `/api/Inventory/remove-stock` | Lấy pin ra khỏi kho | Admin, Staff |
| **Inventory** | `POST` | `/api/Inventory/change-status` | Đổi trạng thái pin trong kho | Admin, Staff |
| **Inventory** | `GET` | `/api/Inventory/summary/station/{stationId}` | Tổng quan kho tại trạm | Admin, Staff |
| **Inventory** | `GET` | `/api/Inventory/all` | Xem tất cả inventory | Admin, Staff |
| **Inventory** | `GET` | `/api/Inventory/health` | Kiểm tra tình trạng kho | Admin, Staff |
| **Inventory** | `GET` | `/api/Inventory/available/station/{stationId}` | Pin khả dụng tại trạm | Admin, Staff |

---

## 10. User Management

### Users CRUD & Management

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Users** | `POST` | `/api/v1/Users` | Admin tạo tài khoản Staff/Driver | Admin |
| **Users** | `GET` | `/api/v1/Users` | Lấy danh sách users (có filter, search, phân trang) | Admin |
| **Users** | `GET` | `/api/v1/Users/customers` | Lấy danh sách khách hàng (Driver) | Admin, Staff |
| **Users** | `GET` | `/api/v1/Users/staff` | Lấy danh sách nhân viên (Staff) | Admin |
| **Users** | `GET` | `/api/v1/Users/staff/{id}` | Xem chi tiết Staff với thống kê công việc | Admin |
| **Users** | `GET` | `/api/v1/Users/{id}` | Xem chi tiết user | Admin |
| **Users** | `PUT` | `/api/v1/Users/{id}` | ⭐ **CẬP NHẬT**: Cập nhật thông tin user (hỗ trợ upload ảnh đại diện). **Staff có thể cập nhật hồ sơ của chính mình** (Name/Phone/Avatar) nhưng không thể thay đổi Role/Status/StationId | Admin, Staff, Driver |
| **Users** | `DELETE` | `/api/v1/Users/{id}` | Xóa user | Admin |
| **Users** | `GET` | `/api/v1/Users/statistics` | Thống kê users (tổng số, active users, new users) | Admin |
| **Users** | `POST` | `/api/v1/Users/change-password` | User đổi mật khẩu của mình | Authorize |

**⭐ CẬP NHẬT MỚI (2025-11-07) - PUT `/api/v1/Users/{id}` Authorization Logic:**

| Người dùng | Được phép cập nhật | Trường hợp đặc biệt |
| :--- | :--- | :--- |
| **Admin** | Tất cả users (không giới hạn trường) | Full access |
| **Staff** | (1) Chính mình: Name, Phone, Avatar **KHÔNG** được đổi Role/Status/StationId<br/>(2) Driver: Name, Phone, Avatar **KHÔNG** được đổi Role/Status<br/>(3) Staff/Admin khác: **KHÔNG** được phép | Nếu Staff cố đổi Role/Status/StationId của mình → 400 Bad Request<br/>Nếu Staff cố cập nhật Staff/Admin khác → 403 Forbidden |
| **Driver** | Chỉ chính mình: Name, Phone, Avatar **KHÔNG** được đổi Role/Status | Nếu Driver cố đổi Role/Status → 400 Bad Request |

---

## 11. Utility APIs

### 11.1. Health & Monitoring

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Health** | `GET` | `/api/Health/ping` | Kiểm tra trạng thái hoạt động của API | Công khai |

### 11.2. File Upload

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **FileUpload** | `POST` | `/api/v1/FileUpload/vehicle-photo` | Upload ảnh xe và trả về URL | Authorize |
| **FileUpload** | `POST` | `/api/v1/FileUpload/registration-photo` | Upload ảnh đăng ký xe và trả về URL | Authorize |

### 11.3. Notifications

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Notifications** | `GET` | `/api/notifications` | Lấy thông báo cho người dùng hiện tại (có phân trang) | Authorize |
| **Notifications** | `POST` | `/api/notifications/{id}/mark-as-read` | Đánh dấu thông báo là đã đọc | Authorize |

### 11.4. Test APIs (Development Only)

| Controller | Phương thức | Endpoint | Chức năng | Quyền truy cập |
| :--- | :--- | :--- | :--- | :--- |
| **Test** | `POST` | `/api/v1/Test/check-email` | Kiểm tra email có tồn tại trong hệ thống không | Development |
| **Test** | `POST` | `/api/v1/Test/send-test-email` | Gửi test email OTP | Development |
| **VnPayTest** | `POST` | `/api/v1/VnPayTest/create-payment` | Test tạo payment VNPay | Development |
| **VnPayTest** | `GET` | `/api/v1/VnPayTest/payment-callback` | Test callback VNPay | Development |

---

## 📊 Tổng hợp theo quyền truy cập

| Quyền truy cập | Số lượng APIs |
| :--- | ---: |
| **Công khai (Public)** | 12 |
| **Authorize (Authenticated)** | 35 |
| **Driver** | 22 |
| **Staff** | 25 |
| **Admin** | 22 |
| **Development** | 4 |
| **Tổng cộng** | **120** |

---

## 🔑 Ghi chú về Authentication

### JWT Token (Cookie-based)
- Token được lưu trong HTTP-only cookie
- Tự động gửi kèm mỗi request
- Bảo mật hơn so với localStorage

### Bearer Token (Header-based)
```
Authorization: Bearer <jwt_token>
```

---

## 🌟 Luồng nghiệp vụ chính

### Flow 1: Đăng ký Subscription
1. `GET /api/v1/subscription-plans` - Xem các gói
2. `POST /api/v1/subscriptions/create-pending` - Tạo subscription pending
3. Redirect đến VNPay để thanh toán
4. `GET /api/v1/payments/vnpay/callback` - VNPay callback cập nhật payment
5. `GET /api/v1/subscriptions/mine` - Xem subscription đã active

### Flow 2: Đặt lịch Pay-per-Swap
1. `GET /api/v1/slot-reservations/available-slots` - Xem slot trống
2. `POST /api/v1/payments/create-pay-per-swap-reservation` - Đặt lịch + thanh toán
3. Nếu VNPay: Redirect thanh toán
4. Nếu Cash: Đợi Staff xác nhận tại trạm

### Flow 3: Check-in và đổi pin
1. Driver đến trạm, show QR code
2. `POST /api/v1/slot-reservations/{id}/check-in` - Staff quét QR
3. Nếu Cash chưa trả: `POST /api/v1/payments/{paymentId}/complete-cash`
4. `POST /api/v1/swaps/finalize-from-reservation` - Hoàn tất đổi pin
5. `PUT /api/v1/swaps/{id}/rate` - Đánh giá (optional)

### Flow 4: Khiếu nại và Re-swap
1. `POST /api/v1/swaps/report-faulty` - Báo cáo pin lỗi
2. `POST /api/driver/complaints/{id}/schedule-inspection` - Đặt lịch kiểm tra
3. `POST /api/BatteryComplaints/{id}/investigate` - Staff điều tra
4. `POST /api/BatteryComplaints/{id}/resolve` - Quyết định re-swap
5. Driver check-in tại slot đã đặt
6. `POST /api/BatteryComplaints/{id}/finalize-reswap` - Hoàn tất re-swap

---

## 📝 Status Codes

| Code | Meaning | Description |
| :--- | :--- | :--- |
| `200` | OK | Request thành công |
| `201` | Created | Tạo resource thành công |
| `204` | No Content | Xóa thành công (không có response body) |
| `400` | Bad Request | Dữ liệu đầu vào không hợp lệ |
| `401` | Unauthorized | Chưa đăng nhập hoặc token không hợp lệ |
| `403` | Forbidden | Không có quyền truy cập |
| `404` | Not Found | Không tìm thấy resource |
| `409` | Conflict | Xung đột dữ liệu (VD: email đã tồn tại) |
| `500` | Internal Server Error | Lỗi server |

---

**© 2025 EV Battery Swap Station Management System**
