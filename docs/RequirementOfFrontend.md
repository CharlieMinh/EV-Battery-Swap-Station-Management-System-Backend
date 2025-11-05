1. Luồng 1: Mua GÓI (Subscription) bằng Tiền mặt
Luồng này đơn giản, chỉ xử lý việc thanh toán, không liên quan đến lịch hẹn.
Khởi tạo: User chọn gói và chọn "Thanh toán Tiền mặt". Hệ thống tạo ra một Payment (với type = 'Subscription', method = 'Cash', status = 'Pending').
Xác nhận (Tại trạm): Staff tìm Payment này và xác nhận đã thu tiền. Payment.Status chuyển thành Completed. UserSubscription được kích hoạt.
Tự động hủy (Timeout):
Rule: Nếu Payment này vẫn Pending quá 72 giờ (3 ngày) kể từ lúc tạo.
Hành động (BE): Một "Scheduled Job" (chạy hàng đêm) sẽ tự động tìm và chuyển Payment.Status = 'Cancelled'.
Hậu quả: Không có hình phạt. User muốn mua thì phải tạo lại giao dịch mới.

--------------------------------------------------------------------------------------

2. Luồng 2: Đặt LỊCH ĐỔI PIN (Reservation)
Đây là luồng nghiệp vụ cốt lõi, xử lý cả 2 hình thức thanh toán (Gói hoặc Tiền mặt) và áp dụng logic phạt "cân bằng".
Bước 1: User Đặt lịch
Khi user chọn slot, xe và nhấn "Xác nhận", hệ thống sẽ rẽ nhánh:
Kịch bản A: User chọn thanh toán bằng GÓI (Logic "Trừ Quota Cân Bằng")
Kiểm tra & Trừ ngay: BE kiểm tra user còn quota (CurrentMonthSwapCount < swapsLimit). Nếu còn, BE tạo Reservation VÀ tăng CurrentMonthSwapCount++ ngay lập tức.
Cảnh báo (FE): Giao diện phải hiển thị rõ: "Lượt đổi pin sẽ bị trừ ngay. Bạn sẽ được hoàn lại nếu hủy lịch trước 1 giờ."
Kịch bản B: User chọn thanh toán bằng TIỀN MẶT (Logic "Phạt Cân Bằng")
Kiểm tra Phạt: BE kiểm tra User.NoShowCount (số lần vi phạm trong 30 ngày qua).
Rule: Nếu User.NoShowCount >= 3 $\rightarrow$ Trả về lỗi 403 (Forbidden), cấm user chọn thanh toán tiền mặt. Yêu cầu họ thanh toán online.
Tạo thanh toán: Nếu NoShowCount < 3, BE tạo Reservation VÀ 1 Payment (với type = 'PayPerSwap', status = 'Pending', và reservationId được liên kết).
Bước 2: Xử lý Hủy lịch (Logic "Cân Bằng" áp dụng cho cả hai)
Kịch bản C: User Tự Hủy lịch
Kiểm tra thời gian: BE so sánh Reservation.StartTime với thời gian hiện tại (DateTime.UtcNow).
Nếu Hủy Sớm (trước giờ hẹn > 1 giờ):
Reservation.Status = Cancelled.
Gói: Hoàn lại quota (CurrentMonthSwapCount--).
Tiền mặt: Hủy Payment liên quan.
Hậu quả: Không có hình phạt.
Nếu Hủy Sát Giờ (trong vòng <= 1 giờ):
Reservation.Status = Cancelled.
Gói: KHÔNG hoàn quota (User mất lượt).
Tiền mặt: Hủy Payment liên quan VÀ áp dụng phạt (User.NoShowCount++).
Cảnh báo (FE): Khi user bấm hủy, Modal xác nhận phải hiển thị rõ (ví dụ: "Hủy sát giờ sẽ bị tính 1 lần vi phạm" hoặc "sẽ không được hoàn quota").
Kịch bản D: Staff Hủy lịch (Lỗi do trạm)
Hành động: Reservation.Status = Cancelled.
Hậu quả:
Gói: Luôn luôn hoàn quota (CurrentMonthSwapCount--).
Tiền mặt: Hủy Payment liên quan.
Hậu quả: Không có hình phạt.
Bước 3: Xử lý Không đến (No-Show)
Rule: Một "Scheduled Job" của BE sẽ quét các Reservation (cả Gói và Tiền mặt) vẫn Pending sau khi đã qua Reservation.EndTime 15 phút.
Hành động:
Chuyển Reservation.Status = 'Cancelled' (No-Show).
Hậu quả:
Gói: KHÔNG hoàn quota (vì đã bị trừ lúc đặt).
Tiền mặt: Hủy Payment liên quan VÀ áp dụng phạt (User.NoShowCount++).

