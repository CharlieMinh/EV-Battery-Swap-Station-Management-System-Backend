# THÔNG TIN TÍCH HỢP VNPAY - MERCHANT SANDBOX

## ⚠️ CẢNH BÁO
**Đây là môi trường SANDBOX của VNPAY, chỉ dùng để test. KHÔNG được sử dụng để đưa ra cho khách hàng thanh toán thật.**

---

## 🔐 THÔNG TIN CẤU HÌNH

### Terminal ID / Mã Website
```
vnp_TmnCode: OE2KYEVL
```

### Secret Key / Chuỗi bí mật tạo checksum
```
vnp_HashSecret: WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S
```

### URL Thanh toán môi trường TEST
```
vnp_Url: https://sandbox.vnpayment.vn/paymentv2/vpcpay.html
```

---

## 🌐 THÔNG TIN TRUY CẬP MERCHANT ADMIN

### Quản lý giao dịch
- **Địa chỉ**: https://sandbox.vnpayment.vn/merchantv2/
- **Tên đăng nhập**: phamvanminh150204@gmail.com
- **Mật khẩu**: (Mật khẩu bạn đã nhập khi đăng ký Merchant môi trường TEST)

---

## 🧪 KIỂM TRA (TEST CASE) - IPN URL

### Kịch bản test (SIT)
- **URL**: https://sandbox.vnpayment.vn/vnpaygw-sit-testing/user/login
- **Tên đăng nhập**: phamvanminh150204@gmail.com
- **Mật khẩu**: (Mật khẩu bạn đã nhập khi đăng ký Merchant môi trường TEST)

---

## 📚 TÀI LIỆU THAM KHẢO

### Tài liệu hướng dẫn tích hợp
https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html

### Code demo tích hợp
https://sandbox.vnpayment.vn/apis/vnpay-demo/code-demo-tích-hợp

---

## 📝 LƯU Ý QUAN TRỌNG

### IPN URL (Server-to-Server Callback)
Merchant cần tạo địa chỉ IPN để VNPAY gọi về cập nhật trạng thái thanh toán.

**Yêu cầu IPN URL:**
- Phải là HTTPS (hoặc HTTP cho localhost test)
- Phải accessible từ internet (dùng ngrok cho localhost)
- Phải trả về JSON response với `RspCode` và `Message`

**IPN URL của chúng ta sẽ là:**
```
https://<your-ngrok-domain>/api/payments/vnpay-ipn
```

### Return URL (User Redirect)
URL để redirect user về sau khi thanh toán xong.

**Return URL của chúng ta:**
```
http://localhost:5173/driver?payment=vnpay
```

---

## 🔧 CẤU HÌNH TRONG appsettings.json

```json
{
  "VnPay": {
    "TmnCode": "OE2KYEVL",
    "HashSecret": "WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S",
    "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
    "Version": "2.1.0",
    "Command": "pay",
    "CurrCode": "VND",
    "Locale": "vn",
    "ReturnUrl": "http://localhost:5173/driver?payment=vnpay",
    "IpnUrl": "https://<your-ngrok-domain>/api/payments/vnpay-ipn"
  }
}
```

---

## ✅ CHECKLIST TRƯỚC KHI BẮT ĐẦU

- [x] Đã có TmnCode: OE2KYEVL
- [x] Đã có HashSecret: WBXOSWTZIWY391QZZSJUGA2AF0D9QS5S
- [ ] Đã tạo IPN URL endpoint
- [ ] Đã expose IPN URL qua ngrok (nếu test local)
- [ ] Đã configure IPN URL trong VnPay Merchant Admin
- [ ] Đã test signature generation
- [ ] Đã test IPN callback handler
- [ ] Đã test payment flow end-to-end

---

## 🚀 BƯỚC TIẾP THEO

1. ✅ Đọc tài liệu VNPay PAY API
2. ✅ Lưu thông tin merchant
3. ⏳ Implement VnPayService mới từ đầu
4. ⏳ Tạo IPN endpoint
5. ⏳ Tạo Return URL handler
6. ⏳ Test với VNPay sandbox
7. ⏳ Configure IPN URL trong VNPay Merchant Admin

---

**Email liên hệ hỗ trợ VNPay**: phamvanminh150204@gmail.com
