# 🎯 HƯỚNG DẪN FRONTEND XỬ LÝ VNPAY RETURN

## 📋 **TỔNG QUAN:**

Sau khi user thanh toán VNPay, VNPay sẽ redirect về:
```
http://localhost:5173/driver?payment=vnpay&vnp_Amount=50000000&vnp_BankCode=NCB&vnp_ResponseCode=00&vnp_TxnRef=EVB20251025123456&...
```

Frontend cần:
1. ✅ Đọc query parameters từ URL
2. ✅ Kiểm tra `vnp_ResponseCode` để biết kết quả
3. ✅ Hiển thị thông báo (toast/modal)
4. ✅ Reload danh sách subscriptions (nếu cần)

---

## 🔧 **CODE MẪU CHO FRONTEND:**

### **Bước 1: Thêm vào `/driver` page** (DriverDashboard.tsx hoặc tương tự)

```typescript
import { useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { toast } from 'react-toastify'; // Hoặc notification library bạn đang dùng

function DriverDashboard() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    // Kiểm tra có query param payment=vnpay không
    if (searchParams.get('payment') === 'vnpay') {
      handleVnPayReturn();
    }
  }, [searchParams]);

  const handleVnPayReturn = () => {
    const responseCode = searchParams.get('vnp_ResponseCode');
    const txnRef = searchParams.get('vnp_TxnRef');
    const amount = searchParams.get('vnp_Amount');

    // Xóa query params để URL sạch
    setSearchParams({});

    // Xử lý theo response code
    if (responseCode === '00') {
      // ✅ THÀNH CÔNG
      const amountVND = amount ? parseInt(amount) / 100 : 0;
      toast.success(
        `Thanh toán thành công! Số tiền: ${amountVND.toLocaleString('vi-VN')} VNĐ`,
        { autoClose: 5000 }
      );
      
      // TODO: Reload subscription list hoặc gọi API để cập nhật UI
      // fetchUserSubscriptions();
    } else {
      // ❌ THẤT BẠI
      const errorMessages: Record<string, string> = {
        '24': 'Bạn đã hủy giao dịch',
        '51': 'Tài khoản không đủ số dư',
        '65': 'Bạn đã vượt quá số lần nhập OTP',
        '75': 'Ngân hàng đang bảo trì',
        '79': 'Giao dịch vượt quá hạn mức',
        '99': 'Lỗi không xác định'
      };
      
      const errorMsg = errorMessages[responseCode || ''] || 'Giao dịch thất bại';
      toast.error(`Thanh toán thất bại: ${errorMsg}`, { autoClose: 5000 });
    }
  };

  return (
    <div>
      {/* Nội dung dashboard của bạn */}
    </div>
  );
}
```

---

## 📝 **VNPAY RESPONSE CODES:**

| Code | Ý nghĩa | Action |
|------|---------|--------|
| `00` | ✅ Thành công | Hiển thị success, reload data |
| `24` | ❌ User hủy | Hiển thị "Bạn đã hủy giao dịch" |
| `51` | ❌ Không đủ tiền | "Tài khoản không đủ số dư" |
| `65` | ❌ OTP sai nhiều lần | "Bạn đã vượt quá số lần nhập OTP" |
| `75` | ❌ Ngân hàng bảo trì | "Ngân hàng đang bảo trì" |
| `79` | ❌ Vượt hạn mức | "Giao dịch vượt quá hạn mức" |
| `99` | ❌ Lỗi khác | "Lỗi không xác định" |

---

## 🔍 **QUERY PARAMETERS TỪ VNPAY:**

VNPay sẽ redirect với các params sau:

```
?payment=vnpay
&vnp_Amount=50000000           // Số tiền (x100, VD: 500000.00 VNĐ = 50000000)
&vnp_BankCode=NCB              // Mã ngân hàng
&vnp_ResponseCode=00           // Mã kết quả (quan trọng nhất!)
&vnp_TxnRef=EVB20251025123456  // Mã giao dịch
&vnp_TransactionNo=14379482    // Mã GD VNPay
&vnp_TransactionStatus=00      // Trạng thái GD
&vnp_OrderInfo=...             // Thông tin đơn hàng
&vnp_PayDate=20251025124530    // Thời gian thanh toán
&vnp_SecureHash=abc123...      // Chữ ký (FE không cần validate, BE đã validate trong IPN)
```

---

## 🚨 **LƯU Ý QUAN TRỌNG:**

### **1. IPN vs ReturnUrl:**
- **IPN (Backend callback)**: VNPay → Backend → Update DB
- **ReturnUrl (Frontend)**: VNPay → Frontend → Hiển thị UI

### **2. Không validate signature ở Frontend:**
Backend đã validate qua IPN callback, Frontend chỉ hiển thị thông báo.

### **3. Xử lý race condition:**
IPN (backend) có thể chạy chậm hơn ReturnUrl (frontend):
```typescript
// Nếu gọi API ngay sau return, có thể subscription chưa active
// Giải pháp: Retry hoặc đợi 1-2 giây
setTimeout(() => {
  fetchUserSubscriptions();
}, 2000);
```

### **4. Test case:**
```
SUCCESS: ?payment=vnpay&vnp_ResponseCode=00&vnp_Amount=50000000&vnp_TxnRef=TEST123
FAILED:  ?payment=vnpay&vnp_ResponseCode=24&vnp_TxnRef=TEST456
```

---

## 📚 **NEXT STEPS:**

1. ✅ Thêm code xử lý vào `/driver` page
2. ✅ Test với test card VNPay:
   ```
   Bank: NCB
   Card: 9704198526191432198
   Name: NGUYEN VAN A
   Issue Date: 07/15
   OTP: 123456
   ```
3. ✅ Verify notification hiển thị đúng
4. ✅ Verify subscription list được reload

---

## 🐛 **TROUBLESHOOTING:**

**Vấn đề:** VNPay báo "Website chưa được phê duyệt"
- **Nguyên nhân:** ReturnUrl không đúng
- **Giải pháp:** ✅ Đã fix, ReturnUrl = `http://localhost:5173/driver?payment=vnpay`

**Vấn đề:** Subscription chưa active sau khi thanh toán
- **Nguyên nhân:** IPN chưa chạy xong
- **Giải pháp:** Đợi 2-3 giây rồi reload, hoặc polling API

**Vấn đề:** Toast không hiện
- **Nguyên nhân:** Chưa import toast hoặc ToastContainer
- **Giải pháp:** Kiểm tra import và render ToastContainer trong App.tsx

---

## 📞 **HỖ TRỢ:**

Nếu gặp vấn đề, cung cấp:
1. Screenshot error
2. URL sau khi VNPay redirect (có query params)
3. Console log (nếu có)
