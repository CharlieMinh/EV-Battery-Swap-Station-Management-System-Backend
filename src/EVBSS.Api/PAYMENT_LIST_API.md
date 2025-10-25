# 📋 API MỚI: STAFF VIEW PAYMENTS

## 🎯 VẤN ĐỀ

Frontend hỏi:
> "Hiện tại không có API nào để Staff xem danh sách Payment (giao dịch) để biết giao dịch nào đã hoàn tất, giao dịch nào chưa."

---

## ✅ GIẢI PHÁP ĐÃ TRIỂN KHAI

Đã thêm **2 API endpoints** mới cho Staff/Admin:

### **1. GET /api/v1/payments** - Lấy danh sách payments (có filter + pagination)
### **2. GET /api/v1/payments/{id}** - Xem chi tiết 1 payment

---

## 📋 API DOCUMENTATION

### **API 1: GET /api/v1/payments**

**Mục đích:** Staff/Admin xem danh sách tất cả giao dịch với filter và phân trang

**Authorization:** `Bearer token` (Role: Staff/Admin)

**Query Parameters:**

| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `pageNumber` | int | No | Số trang (default: 1) | `1` |
| `pageSize` | int | No | Số items/trang (default: 20) | `20` |
| `status` | string | No | Filter theo status | `Pending`, `Completed` |
| `method` | string | No | Filter theo phương thức | `VNPay`, `Cash` |
| `type` | string | No | Filter theo loại | `Subscription`, `PayPerSwap` |
| `userId` | guid | No | Filter theo user | `guid-user-123` |
| `fromDate` | date | No | Từ ngày (yyyy-MM-dd) | `2025-10-01` |
| `toDate` | date | No | Đến ngày (yyyy-MM-dd) | `2025-10-24` |

---

**Response Example:**

```json
{
  "items": [
    {
      "id": "payment-guid-1",
      "amount": 50000,
      "method": "VNPay",
      "type": "PayPerSwap",
      "status": "Completed",
      "createdAt": "2025-10-24T08:00:00Z",
      "completedAt": "2025-10-24T08:05:00Z",
      "userName": "Nguyễn Văn A",
      "userPhone": "0912345678",
      "userEmail": "user@example.com",
      "subscriptionPlanName": null,
      "reservationId": "reservation-guid",
      "processedByStaffName": "Staff B"
    },
    {
      "id": "payment-guid-2",
      "amount": 200000,
      "method": "Cash",
      "type": "Subscription",
      "status": "Pending",
      "createdAt": "2025-10-24T09:00:00Z",
      "completedAt": null,
      "userName": "Trần Thị B",
      "userPhone": "0987654321",
      "userEmail": "userb@example.com",
      "subscriptionPlanName": "Gói Cơ Bản - 1 Tháng",
      "reservationId": null,
      "processedByStaffName": null
    }
  ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 156,
  "totalPages": 8
}
```

---

### **API 2: GET /api/v1/payments/{id}**

**Mục đích:** Staff/Admin xem chi tiết đầy đủ 1 giao dịch

**Authorization:** `Bearer token` (Role: Staff/Admin)

**Path Parameter:**
- `id` (guid) - Payment ID

**Response Example:**

```json
{
  "id": "payment-guid-123",
  "amount": 50000,
  "method": "VNPay",
  "type": "PayPerSwap",
  "status": "Completed",
  "description": "Thanh toán đặt lịch đổi pin - 24/10/2025 09:00-09:30",
  "paymentReference": "EVB20251024090001234",
  "vnpTxnRef": "EVB20251024090001234",
  "vnpTransactionNo": "14123456",
  "vnpResponseCode": "00",
  "createdAt": "2025-10-24T09:00:00Z",
  "completedAt": "2025-10-24T09:05:00Z",
  "userName": "Nguyễn Văn A",
  "userPhone": "0912345678",
  "userEmail": "user@example.com",
  "subscriptionPlanName": null,
  "reservationId": "reservation-guid-456",
  "userSubscriptionId": null,
  "processedByStaffId": "staff-guid-789",
  "processedByStaffName": "Staff B"
}
```

---

## 🔄 FRONTEND INTEGRATION

### **Use Case 1: Hiển thị danh sách payments**

```javascript
async function fetchPayments(filters = {}) {
  const params = new URLSearchParams({
    pageNumber: filters.pageNumber || 1,
    pageSize: filters.pageSize || 20,
    ...(filters.status && { status: filters.status }),
    ...(filters.method && { method: filters.method }),
    ...(filters.type && { type: filters.type }),
    ...(filters.fromDate && { fromDate: filters.fromDate }),
    ...(filters.toDate && { toDate: filters.toDate })
  });

  const response = await fetch(
    `${API_BASE}/v1/payments?${params}`,
    { headers: { Authorization: `Bearer ${staffToken}` }}
  );

  return await response.json();
}

// Example: Lấy tất cả payments hôm nay
const today = new Date().toISOString().split('T')[0];
const payments = await fetchPayments({ 
  fromDate: today, 
  toDate: today 
});

console.log(`Tổng ${payments.totalCount} giao dịch hôm nay`);
```

---

### **Use Case 2: Filter payments theo status**

```javascript
// Lấy các giao dịch chưa hoàn tất (Pending)
const pendingPayments = await fetchPayments({ status: 'Pending' });

pendingPayments.items.forEach(payment => {
  console.log(`⚠️ ${payment.userName} - ${payment.amount.toLocaleString('vi-VN')} VNĐ - Chờ xử lý`);
});
```

---

### **Use Case 3: Filter payments Cash cần xác nhận**

```javascript
// Lấy Cash + Pending
const cashPending = await fetchPayments({ 
  method: 'Cash', 
  status: 'Pending' 
});

console.log(`Có ${cashPending.totalCount} giao dịch Cash cần xác nhận`);
```

---

### **Use Case 4: Xem chi tiết payment**

```javascript
async function getPaymentDetail(paymentId) {
  const response = await fetch(
    `${API_BASE}/v1/payments/${paymentId}`,
    { headers: { Authorization: `Bearer ${staffToken}` }}
  );
  
  if (!response.ok) {
    throw new Error('Payment not found');
  }
  
  return await response.json();
}

// User click vào 1 row trong table
const detail = await getPaymentDetail('payment-guid-123');
console.log('Chi tiết:', detail);
```

---

### **Use Case 5: Pagination**

```javascript
function renderPagination(data) {
  const { pageNumber, totalPages, totalCount } = data;
  
  console.log(`Trang ${pageNumber}/${totalPages} - Tổng ${totalCount} giao dịch`);
  
  // Render buttons
  for (let i = 1; i <= totalPages; i++) {
    const button = document.createElement('button');
    button.textContent = i;
    button.disabled = i === pageNumber;
    button.onclick = () => fetchPayments({ pageNumber: i });
    paginationContainer.appendChild(button);
  }
}
```

---

## 📊 ENUM VALUES

### **PaymentStatus:**

| Value | Display | Icon | Ý nghĩa |
|-------|---------|------|---------|
| `Pending` | Chờ xử lý | 🟡 | Chưa thanh toán |
| `Processing` | Đang xử lý | 🔵 | Đang xử lý |
| `Completed` | Hoàn tất | 🟢 | Đã thanh toán |
| `Failed` | Thất bại | 🔴 | Thanh toán lỗi |
| `Cancelled` | Đã hủy | ⚫ | User hủy |
| `Refunded` | Đã hoàn tiền | 🟣 | Đã hoàn tiền |

---

### **PaymentMethod:**

| Value | Display |
|-------|---------|
| `VNPay` | VNPay (Online) |
| `Cash` | Tiền mặt |
| `BankTransfer` | Chuyển khoản |
| `Momo` | Ví MoMo |

---

### **PaymentType:**

| Value | Display |
|-------|---------|
| `Subscription` | Mua gói subscription |
| `PayPerSwap` | Đặt lịch lẻ (Pay-per-Swap) |

---

## 🎨 UI EXAMPLE (React Component)

```jsx
import React, { useState, useEffect } from 'react';

function PaymentDashboard() {
  const [payments, setPayments] = useState({ items: [], totalCount: 0 });
  const [filters, setFilters] = useState({
    pageNumber: 1,
    pageSize: 20,
    status: '',
    method: '',
    fromDate: '',
    toDate: ''
  });

  useEffect(() => {
    fetchPayments(filters).then(setPayments);
  }, [filters]);

  return (
    <div className="payment-dashboard">
      {/* Filters */}
      <div className="filters">
        <select onChange={e => setFilters({...filters, status: e.target.value})}>
          <option value="">All Status</option>
          <option value="Pending">Pending</option>
          <option value="Completed">Completed</option>
        </select>
        
        <select onChange={e => setFilters({...filters, method: e.target.value})}>
          <option value="">All Methods</option>
          <option value="VNPay">VNPay</option>
          <option value="Cash">Cash</option>
        </select>
        
        <input 
          type="date" 
          onChange={e => setFilters({...filters, fromDate: e.target.value})} 
          placeholder="From Date"
        />
      </div>

      {/* Table */}
      <table>
        <thead>
          <tr>
            <th>Thời gian</th>
            <th>Khách hàng</th>
            <th>Số tiền</th>
            <th>Phương thức</th>
            <th>Trạng thái</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {payments.items.map(payment => (
            <tr key={payment.id}>
              <td>{new Date(payment.createdAt).toLocaleString('vi-VN')}</td>
              <td>{payment.userName}</td>
              <td>{payment.amount.toLocaleString('vi-VN')} VNĐ</td>
              <td>{payment.method}</td>
              <td>
                <span className={`status-${payment.status.toLowerCase()}`}>
                  {payment.status}
                </span>
              </td>
              <td>
                <button onClick={() => viewDetail(payment.id)}>
                  Chi tiết
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* Pagination */}
      <div className="pagination">
        <span>Trang {filters.pageNumber}/{Math.ceil(payments.totalCount / filters.pageSize)}</span>
        <button 
          disabled={filters.pageNumber === 1}
          onClick={() => setFilters({...filters, pageNumber: filters.pageNumber - 1})}
        >
          Trước
        </button>
        <button 
          disabled={filters.pageNumber * filters.pageSize >= payments.totalCount}
          onClick={() => setFilters({...filters, pageNumber: filters.pageNumber + 1})}
        >
          Sau
        </button>
      </div>
    </div>
  );
}
```

---

## 📁 FILES CHANGED

1. ✅ **Dtos/Payments/PaymentResponses.cs** - Thêm `PaymentListItemDto` và `PaymentDetailDto`
2. ✅ **Services/PaymentService.cs** - Thêm methods `GetPaymentsAsync`, `GetPaymentByIdAsync`
3. ✅ **Controllers/PaymentsController.cs** - Thêm endpoints `GET /payments`, `GET /payments/{id}`
4. ✅ **test-payment-list-api.http** - Test file với 10 use cases
5. ✅ **PAYMENT_LIST_API.md** - Full documentation

---

## ✅ BUILD STATUS

```
✅ Build: SUCCESS
✅ Endpoints: 2 new APIs
✅ Authorization: Staff/Admin only
✅ Test file: Created
✅ Documentation: Complete
```

---

## 📞 MESSAGE CHO FRONTEND

```
Hi Frontend Team,

✅ Đã thêm API để Staff xem danh sách Payments!

🎯 2 API MỚI:

1️⃣ GET /api/v1/payments
   - Xem tất cả payments
   - Filter: status, method, type, date range
   - Pagination: pageNumber, pageSize
   - Response: { items, totalCount, totalPages }

2️⃣ GET /api/v1/payments/{id}
   - Xem chi tiết 1 payment
   - Response: Full payment info

📋 USE CASES:
✅ Dashboard: Tổng hợp giao dịch hôm nay
✅ Filter: Cash Pending cần xác nhận
✅ Search: Tìm theo user, date range
✅ Detail: Click row xem chi tiết

📚 DOCUMENTS:
- Test file: test-payment-list-api.http
- Full guide: PAYMENT_LIST_API.md
- 10 use cases với examples

🔒 AUTHORIZATION:
- Chỉ Staff/Admin mới truy cập được
- User role sẽ bị 403 Forbidden

Ready to integrate! 🚀
```

---

## ❓ FAQ

**Q: User có thể xem danh sách payments của mình không?**  
A: KHÔNG. API này chỉ cho Staff/Admin. User chỉ xem được history của chính mình qua endpoint khác.

**Q: Có API nào để export payments ra Excel không?**  
A: Chưa. Nếu cần, Frontend có thể dùng API này (không pagination, lấy hết) và export ở client-side.

**Q: Filter có case-sensitive không?**  
A: Status/Method/Type phải viết đúng chữ hoa/thường (VD: `Pending`, không phải `pending`).

**Q: Pagination có giới hạn `pageSize` tối đa không?**  
A: Hiện tại chưa. Recommend: Frontend set max = 100 để tránh query quá nặng.

---

## 🏆 KẾT LUẬN

### **✅ GIẢI QUYẾT XONG VẤN ĐỀ!**

```
┌──────────────────────────────────────────────────────┐
│  VẤN ĐỀ: Staff không có API xem danh sách Payment   │
├──────────────────────────────────────────────────────┤
│  ✅ API 1: GET /payments (list with filters)        │
│  ✅ API 2: GET /payments/{id} (detail)              │
│  ✅ Filters: status, method, type, date, user       │
│  ✅ Pagination: pageNumber, pageSize                │
│  ✅ Authorization: Staff/Admin only                  │
│  ✅ Test file: 10 use cases                         │
│  ✅ Documentation: Complete                          │
│  ✅ Ready: For FE integration                        │
└──────────────────────────────────────────────────────┘
```

**Staff giờ có thể:**
- ✅ Xem tất cả giao dịch với filter
- ✅ Tìm Cash Pending cần xác nhận
- ✅ Xem chi tiết 1 payment
- ✅ Export data (via pagination)

**Ready for integration!** 🎉
