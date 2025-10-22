# 📊 PHÂN TÍCH CHI TIẾT BẢNG PAYMENTS

## 🎯 MỤC ĐÍCH CHUNG
Bảng `Payments` lưu trữ **TẤT CẢ** các giao dịch thanh toán trong hệ thống, bao gồm:
- Thanh toán subscription (gói tháng)
- Thanh toán lẻ (pay-per-swap)
- Thanh toán qua VNPay, Cash, Bank Transfer, Momo

---

## 📋 CHI TIẾT TỪNG CỘT

### **1. IDENTITY & REFERENCE (3 cột)**

#### `Id` (Guid, Primary Key)
- **Mục đích:** Unique identifier cho mỗi payment record
- **Kiểu dữ liệu:** `uniqueidentifier`
- **Ví dụ:** `a1b2c3d4-e5f6-7890-abcd-ef1234567890`
- **Sử dụng:** Tham chiếu payment từ các bảng khác
- **⚠️ Đánh giá:** ✅ **BẮT BUỘC** - Không thể xóa

#### `PaymentReference` (string, NOT NULL)
- **Mục đích:** Mã tham chiếu giao dịch (human-readable)
- **Format:** `PAY-{yyyyMMdd}-{random}`
- **Ví dụ:** `PAY-20251022-ABC123`
- **Sử dụng:** 
  - Hiển thị cho user trong email, invoice
  - Staff tra cứu giao dịch
  - Support team tìm kiếm nhanh
- **⚠️ Đánh giá:** ✅ **CẦN THIẾT** - Dễ đọc hơn GUID

#### `UserId` (Guid, Foreign Key → Users, NOT NULL)
- **Mục đích:** User nào thực hiện payment
- **Ví dụ:** `user-guid-123`
- **Sử dụng:** Liên kết payment với user
- **⚠️ Đánh giá:** ✅ **BẮT BUỘC** - Không thể xóa

---

### **2. PAYMENT CATEGORIZATION (2 cột)**

#### `Method` (enum PaymentMethod, NOT NULL)
- **Mục đích:** Phương thức thanh toán
- **Giá trị:**
  ```
  0 = VNPay (Ví điện tử/Internet Banking)
  1 = Cash (Tiền mặt tại trạm)
  2 = BankTransfer (Chuyển khoản)
  3 = Momo (Ví MoMo)
  ```
- **Ví dụ:** `Method = 0` (VNPay)
- **Sử dụng:** 
  - Routing logic: VNPay cần verify signature, Cash cần staff confirm
  - Báo cáo: Thống kê theo phương thức
- **⚠️ Đánh giá:** ✅ **BẮT BUỘC** - Xác định cách xử lý payment

#### `Type` (enum PaymentType, NOT NULL)
- **Mục đích:** Loại hình thanh toán
- **Giá trị:**
  ```
  0 = Subscription (Thanh toán gói subscription)
  1 = PayPerSwap (Trả lẻ từng lần swap)
  2 = BuyOutright (Mua pin - CHƯA DÙNG)
  3 = TradeIn (Thu cũ đổi mới - CHƯA DÙNG)
  ```
- **Ví dụ:** `Type = 0` (Subscription)
- **Sử dụng:**
  - Phân biệt payment cho subscription vs pay-per-swap
  - Báo cáo doanh thu theo loại
- **⚠️ Đánh giá:** 
  - ✅ **Subscription (0)** - Đang dùng
  - ✅ **PayPerSwap (1)** - Đang dùng (ít)
  - ❌ **BuyOutright (2)** - KHÔNG DÙNG (có thể xóa)
  - ❌ **TradeIn (3)** - KHÔNG DÙNG (có thể xóa)

---

### **3. PAYMENT DETAILS (3 cột)**

#### `Amount` (decimal(18,2), NOT NULL)
- **Mục đích:** Số tiền thanh toán
- **Đơn vị:** VND (Vietnam Dong)
- **Ví dụ:** `450000.00` (450,000 VND)
- **Sử dụng:**
  - Tính tổng doanh thu
  - Verify với VNPay response
- **⚠️ Đánh giá:** ✅ **BẮT BUỘC** - Core field

#### `Status` (enum PaymentStatus, NOT NULL)
- **Mục đích:** Trạng thái hiện tại của payment
- **Giá trị:**
  ```
  0 = Pending (Chờ thanh toán - vừa tạo)
  1 = Processing (Đang xử lý - VNPay đang check)
  2 = Completed (Thành công - đã nhận tiền)
  3 = Failed (Thất bại - VNPay báo lỗi)
  4 = Cancelled (Đã hủy - user cancel)
  5 = Refunded (Đã hoàn tiền)
  6 = PartiallyPaid (Thanh toán một phần - CHƯA DÙNG)
  ```
- **Flow:**
  ```
  Pending → Processing → Completed ✅
  Pending → Processing → Failed ❌
  Pending → Cancelled ❌
  Completed → Refunded (hoàn tiền)
  ```
- **⚠️ Đánh giá:**
  - ✅ **Pending (0)** - Đang dùng
  - ✅ **Processing (1)** - Đang dùng (VNPay callback)
  - ✅ **Completed (2)** - Đang dùng
  - ✅ **Failed (3)** - Đang dùng
  - ✅ **Cancelled (4)** - Đang dùng
  - ⚠️ **Refunded (5)** - CÓ THỂ DÙNG (nếu có chức năng hoàn tiền)
  - ❌ **PartiallyPaid (6)** - KHÔNG DÙNG (có thể xóa)

#### `Description` (string, NOT NULL)
- **Mục đích:** Mô tả giao dịch (cho user)
- **Ví dụ:** 
  - `"Thanh toán gói Basic - tháng 10/2025"`
  - `"Trả phí đổi pin lẻ tại Trạm Quận 1"`
- **Sử dụng:**
  - Hiển thị trong lịch sử giao dịch
  - Email notification
  - Invoice/Receipt
- **⚠️ Đánh giá:** ✅ **CẦN THIẾT** - User cần biết họ trả tiền cho gì

---

### **4. VNPAY INTEGRATION (5 cột) - ĐẶC BIỆT QUAN TRỌNG**

#### `VnpTxnRef` (string, Nullable)
- **Mục đích:** Mã giao dịch tham chiếu gửi cho VNPay
- **Format:** `PAY{yyyyMMddHHmmss}` (VD: `PAY20251022153045`)
- **Ví dụ:** `PAY20251022153045`
- **Sử dụng:**
  - Tracking payment qua VNPay
  - Đối chiếu với VNPay khi có vấn đề
- **⚠️ Đánh giá:** 
  - ✅ **BẮT BUỘC** nếu `Method = VNPay`
  - ❓ **NULL** nếu `Method = Cash/BankTransfer`

#### `VnpTransactionNo` (string, Nullable)
- **Mục đích:** Mã giao dịch do VNPay trả về (sau khi thanh toán)
- **Ví dụ:** `14012345` (VNPay tự sinh)
- **Sử dụng:**
  - Chứng minh payment đã được VNPay xác nhận
  - Đối chiếu khi user khiếu nại
- **⚠️ Đánh giá:**
  - ✅ **CẦN THIẾT** nếu `Method = VNPay` và `Status = Completed`
  - ❓ **NULL** nếu chưa thanh toán hoặc không dùng VNPay

#### `VnpResponseCode` (string, Nullable)
- **Mục đích:** Mã phản hồi từ VNPay
- **Giá trị phổ biến:**
  ```
  "00" = Success (Thành công)
  "24" = User hủy giao dịch
  "09" = Chưa đăng ký InternetBanking
  "51" = Không đủ số dư
  "65" = OTP sai quá số lần
  "75" = Ngân hàng đang bảo trì
  ```
- **Sử dụng:**
  - Xác định lý do thất bại
  - Hiển thị message cho user
  - Báo cáo tỷ lệ thành công/thất bại
- **⚠️ Đánh giá:**
  - ✅ **CẦN THIẾT** nếu `Method = VNPay`
  - ❓ **NULL** nếu không dùng VNPay

#### `VnpSecureHash` (string, Nullable)
- **Mục đích:** Chữ ký điện tử từ VNPay (bảo mật)
- **Ví dụ:** `abc123def456...` (SHA256 hash)
- **Sử dụng:**
  - **VERIFY** callback từ VNPay là thật (không bị giả mạo)
  - Bảo mật: Chỉ VNPay mới tạo được hash đúng
- **⚠️ Đánh giá:**
  - ✅ **BẮT BUỘC** nếu `Method = VNPay` (bảo mật)
  - ❓ **NULL** nếu không dùng VNPay
  - ⚠️ **KHÔNG NÊN XÓA** - Cần cho audit trail

#### `VnpPayDate` (DateTime, Nullable)
- **Mục đích:** Thời gian thanh toán do VNPay trả về
- **Format:** `yyyyMMddHHmmss` (VD: `20251022153045`)
- **Ví dụ:** `2025-10-22 15:30:45`
- **Sử dụng:**
  - So sánh với `CreatedAt` để tính thời gian user hoàn tất
  - Đối chiếu với VNPay nếu có tranh chấp
- **⚠️ Đánh giá:**
  - ✅ **CẦN THIẾT** nếu `Method = VNPay` và `Status = Completed`
  - ❓ **NULL** nếu chưa thanh toán hoặc không dùng VNPay

---

### **5. CASH PAYMENT FIELDS (2 cột) - CHO STAFF**

#### `ProcessedByStaffId` (Guid, Nullable, Foreign Key → Users)
- **Mục đích:** Staff nào xác nhận payment (khi Method = Cash)
- **Ví dụ:** `staff-guid-456`
- **Sử dụng:**
  - Tracking: Staff nào nhận tiền
  - Audit: Đối chiếu cuối ngày
  - Báo cáo: Doanh thu theo staff
- **⚠️ Đánh giá:**
  - ✅ **CẦN THIẾT** nếu `Method = Cash`
  - ❓ **NULL** nếu `Method = VNPay/Momo` (online payment)

#### `StationId` (Guid, Nullable, Foreign Key → Stations)
- **Mục đích:** Trạm nào nhận tiền (khi Method = Cash)
- **Ví dụ:** `station-guid-789`
- **Sử dụng:**
  - Tracking: Doanh thu theo trạm
  - Báo cáo: Station revenue report
- **⚠️ Đánh giá:**
  - ✅ **CẦN THIẾT** nếu `Method = Cash`
  - ❓ **NULL** nếu online payment
  - ⚠️ **CÓ THỂ SÁT NHẬP** với logic khác (xem phần tối ưu)

---

### **6. TIMESTAMPS (3 cột)**

#### `CreatedAt` (DateTime, NOT NULL)
- **Mục đích:** Thời điểm tạo payment record
- **Default:** `GETUTCDATE()`
- **Ví dụ:** `2025-10-22 15:00:00 UTC`
- **Sử dụng:**
  - Tracking: Khi nào user bắt đầu thanh toán
  - Báo cáo: Doanh thu theo thời gian
  - Sort: Lịch sử giao dịch
- **⚠️ Đánh giá:** ✅ **BẮT BUỘC** - Core timestamp

#### `ProcessedAt` (DateTime, Nullable)
- **Mục đích:** Thời điểm payment được xử lý (Processing → Completed/Failed)
- **Ví dụ:** `2025-10-22 15:05:00 UTC`
- **Sử dụng:**
  - Tính thời gian xử lý: `ProcessedAt - CreatedAt`
  - Tracking: Khi nào VNPay callback
- **⚠️ Đánh giá:**
  - ⚠️ **TRÙNG LẶP** với `CompletedAt`?
  - ❓ **CÓ THỂ XÓA** nếu không cần phân biệt Processing/Completed

#### `CompletedAt` (DateTime, Nullable)
- **Mục đích:** Thời điểm payment hoàn tất (Status = Completed)
- **Ví dụ:** `2025-10-22 15:05:00 UTC`
- **Sử dụng:**
  - Tracking: Khi nào nhận được tiền
  - Báo cáo: Doanh thu thực tế (chỉ tính Completed)
- **⚠️ Đánh giá:**
  - ✅ **CẦN THIẾT** - Xác định revenue timing
  - ⚠️ **CÓ THỂ MERGE** với `ProcessedAt`

---

### **7. ADDITIONAL INFO (2 cột)**

#### `Notes` (string, Nullable)
- **Mục đích:** Ghi chú thêm (do staff hoặc system)
- **Ví dụ:** 
  - `"User yêu cầu hoàn tiền vì lỗi hệ thống"`
  - `"Thanh toán tại trạm, staff nhận tiền mặt"`
- **Sử dụng:**
  - Support: Ghi chú cho support team
  - Audit: Lý do đặc biệt
- **⚠️ Đánh giá:**
  - ✅ **HỮU ÍCH** nhưng không bắt buộc
  - ⚠️ **CÓ THỂ GIỮ** cho debugging/support

#### `FailureReason` (string, Nullable)
- **Mục đích:** Lý do thất bại (nếu Status = Failed)
- **Ví dụ:** 
  - `"Không đủ số dư (VNPay code 51)"`
  - `"User hủy giao dịch (VNPay code 24)"`
- **Sử dụng:**
  - Hiển thị cho user: Tại sao payment failed
  - Báo cáo: Nguyên nhân thất bại phổ biến
- **⚠️ Đánh giá:**
  - ⚠️ **TRÙNG LẶP** với `VnpResponseCode`?
  - ❓ **CÓ THỂ DERIVE** từ `VnpResponseCode` (không cần lưu)

---

### **8. RELATIONSHIP (1 cột)**

#### `UserSubscriptionId` (Guid, Nullable, Foreign Key → UserSubscriptions)
- **Mục đích:** Payment này cho subscription nào (nếu Type = Subscription)
- **Ví dụ:** `subscription-guid-abc`
- **Sử dụng:**
  - Link payment với subscription
  - Tracking: User đã trả tiền cho tháng nào
  - Activate subscription sau khi payment completed
- **⚠️ Đánh giá:**
  - ✅ **BẮT BUỘC** nếu `Type = Subscription`
  - ❓ **NULL** nếu `Type = PayPerSwap`

---

## 📊 TỔNG KẾT - PHÂN LOẠI CỘT

### ✅ **CORE FIELDS - BẮT BUỘC (10 cột):**
1. `Id` - Primary Key
2. `PaymentReference` - Human-readable ID
3. `UserId` - Ai thanh toán
4. `Method` - VNPay/Cash/Momo
5. `Type` - Subscription/PayPerSwap
6. `Amount` - Số tiền
7. `Status` - Pending/Completed/Failed
8. `Description` - Mô tả cho user
9. `CreatedAt` - Thời gian tạo
10. `CompletedAt` - Thời gian hoàn tất

**→ KHÔNG THỂ XÓA, cần cho business logic**

---

### ✅ **VNPAY REQUIRED - BẮT BUỘC CHO VNPAY (5 cột):**
11. `VnpTxnRef` - Mã gửi VNPay
12. `VnpTransactionNo` - Mã VNPay trả về
13. `VnpResponseCode` - Kết quả (00=success)
14. `VnpSecureHash` - Bảo mật
15. `VnpPayDate` - Thời gian VNPay confirm

**→ CẦN GIỮ nếu dùng VNPay (nullable OK)**

---

### ⚠️ **CONDITIONAL FIELDS - CẦN TRONG MỘT SỐ CASE (2 cột):**
16. `ProcessedByStaffId` - Nếu Method = Cash
17. `StationId` - Nếu Method = Cash tại trạm

**→ GIỮ nhưng có thể refactor logic**

---

### ⚠️ **OPTIONAL/REDUNDANT - CÓ THỂ XÓA/MERGE (4 cột):**
18. `ProcessedAt` - **TRÙNG** với `CompletedAt`? → **CÓ THỂ XÓA**
19. `Notes` - **HỮU ÍCH** nhưng không critical → **GIỮ**
20. `FailureReason` - **DERIVE ĐƯỢC** từ `VnpResponseCode` → **CÓ THỂ XÓA**
21. `UserSubscriptionId` - **CẦN** cho Subscription type → **GIỮ**

---

## 🎯 ĐỀ XUẤT TỐI ƯU HÓA

### **Loại 1: XÓA HOÀN TOÀN (2 cột)**
```sql
-- Enum values không dùng (BuyOutright, TradeIn, PartiallyPaid)
-- Không cần migration vì đây là enum, chỉ xóa trong code

-- Cột có thể xóa:
ALTER TABLE Payments DROP COLUMN ProcessedAt;  -- Dùng CompletedAt thay thế
ALTER TABLE Payments DROP COLUMN FailureReason; -- Dùng VnpResponseCode + mapping
```

### **Loại 2: REFACTOR LOGIC (2 cột)**
```sql
-- ProcessedByStaffId và StationId:
-- Có thể thay bằng relationship qua SwapTransaction
-- Nếu payment liên kết với swap → lấy station từ swap
```

### **Loại 3: GIỮ NGUYÊN (16 cột)**
Tất cả các cột còn lại cần thiết cho business logic

---

## 🤔 CÂU HỎI CHO BẠN:

1. **PayPerSwap usage:** Hiện tại có dùng `Type = PayPerSwap` không? Hay tất cả đều dùng Subscription?

2. **Cash payment:** Có staff nhận tiền mặt tại trạm không? Hay 100% VNPay online?

3. **Refund:** Có chức năng hoàn tiền không? Hay `Status = Refunded` chưa dùng?

4. **ProcessedAt vs CompletedAt:** Có cần phân biệt "đang xử lý" và "hoàn tất" không?

5. **FailureReason:** Bạn có cần lưu text message hay chỉ code number (`VnpResponseCode`) là đủ?

**→ Trả lời giúp tôi để đưa ra phương án tối ưu chính xác nhất! 🎯**
