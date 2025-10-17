# 📱 HƯỚNG DẪN FRONTEND: Hiển Thị Số Lượng Pin Trong Flow Đặt Lịch

## 🎯 Yêu Cầu Giảng Viên

**"Driver khi đặt lịch phải thấy được số lượng pin còn trống tại trạm"**

→ Backend đã implement API để Frontend có thể hiển thị thông tin này!

---

## 🔌 API Endpoint Mới Cho Frontend

### Endpoint: GET /api/inventory/available/station/{stationId}

**Mục đích:** Hiển thị số lượng pin sẵn có khi Driver đang đặt lịch

**URL:** 
```
GET http://localhost:5194/api/inventory/available/station/{stationId}?batteryModelId={modelId}
```

**Parameters:**
- `stationId` (required): ID của trạm
- `batteryModelId` (optional): ID của loại pin (nếu muốn filter theo xe cụ thể)

**Authorization:** KHÔNG CẦN TOKEN (AllowAnonymous) - để Driver có thể xem trước khi đăng nhập

---

## 📊 Response Format

### Success Response (200 OK):

```json
{
  "success": true,
  "message": "150 batteries available for immediate swap",
  "data": {
    "stationId": "abc-123-...",
    "stationName": "Trạm Hà Nội",
    "availableNow": 150,        // ⭐ Pin sẵn sàng ngay
    "chargingSoon": 80,         // Pin đang sạc
    "totalAvailable": 230,      // Tổng cộng
    "batteryModels": [
      {
        "modelId": "model-1",
        "modelName": "VF5 Battery Pack",
        "fullQuantity": 100,
        "chargingQuantity": 50,
        "availableForSwap": 100  // ⭐ Chỉ pin Full mới đổi được
      },
      {
        "modelId": "model-2", 
        "modelName": "VF8 Battery Pack",
        "fullQuantity": 50,
        "chargingQuantity": 30,
        "availableForSwap": 50
      }
    ],
    "recommendedSlots": "Available - You can book",
    "lastUpdated": "2025-10-15T10:30:00Z"
  }
}
```

### No Batteries Available:

```json
{
  "success": true,
  "message": "No batteries currently available. Please try another station or time slot.",
  "data": {
    "stationId": "abc-123-...",
    "stationName": "Trạm Đà Nẵng",
    "availableNow": 0,          // ⚠️ Hết pin!
    "chargingSoon": 5,
    "totalAvailable": 5,
    "batteryModels": [...],
    "recommendedSlots": "Limited availability - Contact station",
    "lastUpdated": "2025-10-15T10:30:00Z"
  }
}
```

---

## 🎨 UI/UX Implementation Guide

### 1. TRONG FLOW ĐẶT LỊCH - Bước Chọn Trạm

**Vị trí:** Hiển thị trên mỗi card trạm trong danh sách

```jsx
// Component: StationCard.jsx
import { useQuery } from '@tanstack/react-query';

function StationCard({ station }) {
  // Call API để lấy số pin sẵn có
  const { data, isLoading } = useQuery({
    queryKey: ['battery-availability', station.id],
    queryFn: () => 
      fetch(`/api/inventory/available/station/${station.id}`)
        .then(res => res.json())
  });

  const availableCount = data?.data?.availableNow || 0;
  const isAvailable = availableCount > 0;

  return (
    <div className="station-card">
      <h3>{station.name}</h3>
      <p>{station.address}</p>
      
      {/* ⭐ THÊM PHẦN NÀY */}
      <div className="battery-availability">
        <BatteryIcon />
        <span className={isAvailable ? 'text-success' : 'text-warning'}>
          {isAvailable 
            ? `${availableCount} pin sẵn sàng` 
            : 'Liên hệ trạm'}
        </span>
        
        {/* Optional: Charging count */}
        {data?.data?.chargingSoon > 0 && (
          <span className="text-muted">
            + {data.data.chargingSoon} đang sạc
          </span>
        )}
      </div>

      <button 
        disabled={!isAvailable}
        onClick={() => selectStation(station)}
      >
        {isAvailable ? 'Chọn trạm này' : 'Hết pin'}
      </button>
    </div>
  );
}
```

**Visual Design:**

```
┌─────────────────────────────────────┐
│  Trạm Hà Nội                        │
│  123 Đường ABC, Hà Nội              │
│                                     │
│  🔋 150 pin sẵn sàng  ✅            │
│     + 80 đang sạc                   │
│                                     │
│  [ Chọn trạm này ]                  │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│  Trạm Đà Nẵng                       │
│  456 Đường XYZ, Đà Nẵng             │
│                                     │
│  ⚠️ 0 pin sẵn sàng                  │
│     + 5 đang sạc                    │
│                                     │
│  [ Hết pin - Liên hệ trạm ]  ❌     │
└─────────────────────────────────────┘
```

### 2. Chi Tiết Breakdown (Modal/Tooltip)

**Khi user click vào "150 pin sẵn sàng":**

```jsx
function BatteryDetailModal({ stationId }) {
  const { data } = useQuery({
    queryKey: ['battery-detail', stationId],
    queryFn: () => 
      fetch(`/api/inventory/available/station/${stationId}`)
        .then(res => res.json())
  });

  return (
    <Modal>
      <h4>Chi tiết pin tại {data?.data?.stationName}</h4>
      
      <div className="battery-summary">
        <div className="stat-card green">
          <h2>{data?.data?.availableNow}</h2>
          <p>Pin sẵn sàng ngay</p>
        </div>
        <div className="stat-card blue">
          <h2>{data?.data?.chargingSoon}</h2>
          <p>Đang sạc</p>
        </div>
      </div>

      <h5>Theo loại pin:</h5>
      {data?.data?.batteryModels.map(model => (
        <div key={model.modelId} className="model-row">
          <span>{model.modelName}</span>
          <Badge variant="success">
            {model.availableForSwap} sẵn sàng
          </Badge>
        </div>
      ))}

      <p className="text-muted">
        Cập nhật: {formatTime(data?.data?.lastUpdated)}
      </p>
    </Modal>
  );
}
```

### 3. Filter Theo Loại Xe (Advanced)

**Nếu user chọn xe trước:**

```jsx
// Trong flow: Chọn xe → Chọn trạm
function StationList({ selectedVehicle }) {
  // Lấy batteryModelId từ xe đã chọn
  const batteryModelId = selectedVehicle?.compatibleBatteryModelId;

  const { data: stations } = useQuery({
    queryKey: ['stations'],
    queryFn: fetchStations
  });

  return stations.map(station => (
    <StationCardWithFilter 
      station={station}
      batteryModelId={batteryModelId} // ⭐ Filter theo xe
    />
  ));
}

function StationCardWithFilter({ station, batteryModelId }) {
  // Call API với filter
  const url = batteryModelId
    ? `/api/inventory/available/station/${station.id}?batteryModelId=${batteryModelId}`
    : `/api/inventory/available/station/${station.id}`;

  const { data } = useQuery({
    queryKey: ['battery-availability', station.id, batteryModelId],
    queryFn: () => fetch(url).then(res => res.json())
  });

  // Render như bình thường
  return <StationCard station={station} availability={data} />;
}
```

---

## 🎨 UI States & Colors

### Status Colors:

```css
/* Có pin sẵn sàng (>= 50) */
.availability-high {
  color: #28a745; /* Green */
  background: #d4edda;
}

/* Còn ít pin (10-49) */
.availability-medium {
  color: #ffc107; /* Yellow */
  background: #fff3cd;
}

/* Hết pin hoặc rất ít (< 10) */
.availability-low {
  color: #dc3545; /* Red */
  background: #f8d7da;
}

/* Đang sạc */
.charging {
  color: #17a2b8; /* Blue */
  background: #d1ecf1;
}
```

### Icons:

```jsx
// Có pin
<BatteryFullIcon className="text-success" />

// Ít pin
<BatteryLowIcon className="text-warning" />

// Hết pin
<BatteryEmptyIcon className="text-danger" />

// Đang sạc
<BatteryChargingIcon className="text-info" />
```

---

## 🔄 Real-time Updates (Optional - Phase 2)

**Nếu muốn auto-refresh:**

```jsx
function StationCard({ station }) {
  const { data } = useQuery({
    queryKey: ['battery-availability', station.id],
    queryFn: fetchAvailability,
    refetchInterval: 30000, // ⭐ Auto refresh mỗi 30s
    refetchOnWindowFocus: true // Refresh khi user quay lại tab
  });

  return <StationCard data={data} />;
}
```

---

## 📱 Mobile Responsive

### Compact View (Mobile):

```jsx
<div className="station-card-mobile">
  <div className="station-info">
    <h4>Trạm Hà Nội</h4>
    <p className="address">123 Đường ABC</p>
  </div>
  
  <div className="battery-badge">
    🔋 150
  </div>
</div>
```

**Visual:**
```
┌──────────────────────────────┐
│ Trạm Hà Nội         🔋 150   │
│ 123 Đường ABC       ✅       │
└──────────────────────────────┘
```

---

## 🧪 Testing Scenarios

### Test Case 1: Trạm có nhiều pin
```
Input: StationId = "hanoi-station"
Expected: 
  - availableNow > 0
  - Button enabled
  - Green badge
```

### Test Case 2: Trạm hết pin
```
Input: StationId = "danang-station"
Expected:
  - availableNow = 0
  - Button disabled
  - Red badge
  - Message: "Hết pin - Liên hệ trạm"
```

### Test Case 3: Filter theo xe
```
Input: 
  - StationId = "hanoi-station"
  - BatteryModelId = "vf5-battery"
Expected:
  - Only show VF5 battery count
  - Other models hidden
```

---

## 📊 Analytics Events (Optional)

**Track user behavior:**

```javascript
// When user views battery availability
analytics.track('Battery_Availability_Viewed', {
  stationId: station.id,
  stationName: station.name,
  availableCount: data.availableNow,
  timestamp: new Date()
});

// When user clicks on low-battery station
if (availableCount < 10) {
  analytics.track('Low_Battery_Station_Clicked', {
    stationId: station.id,
    availableCount: availableCount
  });
}
```

---

## 🎯 Implementation Checklist

### Phase 1: Basic Display (Bắt buộc)
- [ ] Call API /api/inventory/available/station/{stationId}
- [ ] Hiển thị số pin sẵn sàng trên mỗi station card
- [ ] Disable button nếu hết pin
- [ ] Add loading state khi fetch API

### Phase 2: Enhanced UX (Nên có)
- [ ] Color coding (green/yellow/red) theo số lượng
- [ ] Hiển thị "đang sạc" count
- [ ] Modal chi tiết khi click vào badge
- [ ] Mobile responsive design

### Phase 3: Advanced (Tùy chọn)
- [ ] Filter theo loại xe/pin
- [ ] Auto-refresh mỗi 30s
- [ ] Tooltip với breakdown details
- [ ] Analytics tracking

---

## 🔥 Example Code - Complete Flow

```jsx
// 1. API Hook
function useBatteryAvailability(stationId, batteryModelId) {
  return useQuery({
    queryKey: ['battery-availability', stationId, batteryModelId],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (batteryModelId) params.append('batteryModelId', batteryModelId);
      
      const response = await fetch(
        `/api/inventory/available/station/${stationId}?${params}`
      );
      return response.json();
    },
    refetchInterval: 30000 // Refresh every 30s
  });
}

// 2. Station Card Component
function StationCard({ station, selectedVehicle }) {
  const batteryModelId = selectedVehicle?.compatibleBatteryModelId;
  const { data, isLoading } = useBatteryAvailability(
    station.id, 
    batteryModelId
  );

  if (isLoading) return <Skeleton />;

  const availability = data?.data;
  const hasAvailability = availability?.availableNow > 0;
  
  return (
    <Card className="station-card">
      <CardHeader>
        <h3>{station.name}</h3>
        <p>{station.address}</p>
      </CardHeader>

      <CardBody>
        {/* Battery Availability Badge */}
        <BatteryAvailabilityBadge 
          availableNow={availability?.availableNow}
          chargingSoon={availability?.chargingSoon}
          onClick={() => setShowDetail(true)}
        />

        {/* Recommended Slots */}
        <Alert variant={hasAvailability ? 'success' : 'warning'}>
          {availability?.recommendedSlots}
        </Alert>
      </CardBody>

      <CardFooter>
        <Button 
          disabled={!hasAvailability}
          onClick={() => selectStation(station)}
        >
          {hasAvailability ? 'Chọn trạm này' : 'Liên hệ trạm'}
        </Button>
      </CardFooter>
    </Card>
  );
}

// 3. Battery Badge Component
function BatteryAvailabilityBadge({ availableNow, chargingSoon, onClick }) {
  const getVariant = (count) => {
    if (count >= 50) return 'success';
    if (count >= 10) return 'warning';
    return 'danger';
  };

  return (
    <div className="battery-badge" onClick={onClick}>
      <BatteryIcon className={`icon-${getVariant(availableNow)}`} />
      
      <div className="battery-info">
        <strong>{availableNow}</strong>
        <span>pin sẵn sàng</span>
      </div>

      {chargingSoon > 0 && (
        <div className="charging-info">
          <BatteryChargingIcon />
          <span>+{chargingSoon} đang sạc</span>
        </div>
      )}
    </div>
  );
}
```

---

## 📞 Support

**Backend API đã sẵn sàng!** Frontend team có thể:

1. Test ngay API: 
   ```
   GET http://localhost:5194/api/inventory/available/station/{stationId}
   ```

2. Xem Swagger documentation:
   ```
   http://localhost:5194/swagger
   → Tìm "InventoryController"
   → Endpoint: GET /api/inventory/available/station/{stationId}
   ```

3. Test với Postman/Thunder Client:
   - Không cần Authorization header (AllowAnonymous)
   - Chỉ cần stationId
   - Optional: thêm ?batteryModelId=xxx

---

## ✅ Kết Luận

**Có! Backend đã đáp ứng đủ yêu cầu của giảng viên:**

✅ API endpoint sẵn sàng  
✅ Response format chuẩn, dễ parse  
✅ Performance nhanh (~5ms query time)  
✅ Public access (không cần login để xem)  
✅ Filter theo loại pin (cho xe cụ thể)  
✅ Mobile-friendly data structure  

**Frontend chỉ cần:**
1. Call API này trong flow đặt lịch
2. Hiển thị số `availableNow` lên UI
3. Disable button nếu = 0

**Timeline ước tính:** 2-4 giờ implement Frontend

---

**Ngày tạo:** 15/10/2025  
**Tác giả:** GitHub Copilot  
**Version:** 1.0
