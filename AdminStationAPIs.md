# Admin Station Management APIs

## Endpoints

### 1. GET /api/v1/admin/stations
**Mô tả**: Lấy danh sách tất cả trạm với thông tin chi tiết cho admin

**Query Parameters**:
- `page` (int, default: 1): Trang hiện tại
- `pageSize` (int, default: 20, max: 100): Số items per trang  
- `includeInactive` (bool, default: true): Có bao gồm trạm bị deactivate hay không

**Response**: `PagedResult<AdminStationDto>`
```json
{
  "items": [
    {
      "id": "guid",
      "name": "Station Name",
      "address": "123 Street",
      "city": "HCM", 
      "lat": 10.77,
      "lng": 106.7,
      "isActive": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "totalBatteryUnits": 10,
      "fullBatteryUnits": 8,
      "chargingBatteryUnits": 1,
      "maintenanceBatteryUnits": 1
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 50
}
```

### 2. PUT /api/v1/admin/stations/{id}
**Mô tả**: Cập nhật thông tin trạm (partial update)

**Body**: `UpdateStationRequest` (tất cả fields optional)
```json
{
  "name": "New Station Name",
  "address": "New Address", 
  "city": "HCM",
  "lat": 10.77,
  "lng": 106.7,
  "isActive": false
}
```

**Response**: 
```json
{
  "message": "Station updated successfully",
  "stationId": "guid"
}
```

### 3. PUT /api/v1/admin/stations/{id}/deactivate
**Mô tả**: Deactivate trạm

**Response**:
```json
{
  "message": "Station deactivated successfully", 
  "stationId": "guid"
}
```

### 4. PUT /api/v1/admin/stations/{id}/activate  
**Mô tả**: Activate trạm

**Response**:
```json
{
  "message": "Station activated successfully",
  "stationId": "guid" 
}
```

### 5. GET /api/v1/admin/stations/health
**Mô tả**: Monitor tình trạng SoH (State of Health) của tất cả trạm

**Response**: `Array<StationHealthDto>`
```json
[
  {
    "stationId": "guid",
    "stationName": "Station Name",
    "isActive": true,
    "healthPercentage": 90.0,
    "totalUnits": 10,
    "availableUnits": 8,
    "chargingUnits": 1,
    "maintenanceUnits": 1,
    "lastUpdated": "2024-01-01T00:00:00Z"
  }
]
```

## Authorization
Tất cả endpoints yêu cầu:
- Authentication (Bearer token)
- Role: Admin

## Error Responses
```json
{
  "error": {
    "code": "STATION_NOT_FOUND",
    "message": "Station not found"
  }
}
```