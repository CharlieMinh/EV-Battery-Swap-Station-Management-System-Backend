# PowerShell script để test Battery Units API
# Chạy script này sau khi server đã start

$baseUrl = "http://localhost:5194/api"

# STEP 1: Login để lấy token
Write-Host "=== STEP 1: Login ===" -ForegroundColor Green
$loginBody = @{
    email = "admin@evbss.local"
    password = "12345678Swp@"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-RestMethod -Uri "$baseUrl/Auth/login" -Method POST -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.token
    Write-Host "✅ Login thành công!" -ForegroundColor Green
    Write-Host "Token: $($token.Substring(0,20))..." -ForegroundColor Yellow
} catch {
    Write-Host "❌ Login thất bại: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# STEP 2: Lấy danh sách stations
Write-Host "`n=== STEP 2: Lấy danh sách stations ===" -ForegroundColor Green
try {
    $stations = Invoke-RestMethod -Uri "$baseUrl/v1/Stations" -Method GET -Headers $headers
    $stationId = $stations.items[0].id
    Write-Host "✅ Lấy stations thành công!" -ForegroundColor Green
    Write-Host "Station ID: $stationId" -ForegroundColor Yellow
} catch {
    Write-Host "❌ Lỗi lấy stations: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

# STEP 3: Lấy danh sách battery models
Write-Host "`n=== STEP 3: Lấy danh sách battery models ===" -ForegroundColor Green
try {
    $batteryModels = Invoke-RestMethod -Uri "$baseUrl/BatteryModels" -Method GET -Headers $headers
    $batteryModelId = $batteryModels[0].id
    Write-Host "✅ Lấy battery models thành công!" -ForegroundColor Green
    Write-Host "Battery Model ID: $batteryModelId" -ForegroundColor Yellow
} catch {
    Write-Host "❌ Lỗi lấy battery models: $($_.Exception.Message)" -ForegroundColor Red
    exit
}

# STEP 4: Tạo pin đơn lẻ
Write-Host "`n=== STEP 4: Tạo pin đơn lẻ ===" -ForegroundColor Green
$batteryData = @{
    serial = "BAT001-PS-TEST"
    batteryModelId = $batteryModelId
    stationId = $stationId
} | ConvertTo-Json

try {
    $newBattery = Invoke-RestMethod -Uri "$baseUrl/BatteryUnits" -Method POST -Body $batteryData -Headers $headers
    Write-Host "✅ Tạo pin thành công!" -ForegroundColor Green
    Write-Host "Pin ID: $($newBattery.data.id)" -ForegroundColor Yellow
} catch {
    Write-Host "❌ Lỗi tạo pin: $($_.Exception.Message)" -ForegroundColor Red
}

# STEP 5: Thêm nhiều pin vào trạm
Write-Host "`n=== STEP 5: Thêm nhiều pin vào trạm ===" -ForegroundColor Green
$bulkBatteryData = @{
    stationId = $stationId
    batteryUnits = @(
        @{
            serial = "BAT002-PS-BULK"
            batteryModelId = $batteryModelId
        },
        @{
            serial = "BAT003-PS-BULK"
            batteryModelId = $batteryModelId
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $bulkResult = Invoke-RestMethod -Uri "$baseUrl/BatteryUnits/add-to-station" -Method POST -Body $bulkBatteryData -Headers $headers
    Write-Host "✅ Thêm nhiều pin thành công!" -ForegroundColor Green
    Write-Host "Số pin đã thêm: $($bulkResult.data.Count)" -ForegroundColor Yellow
} catch {
    Write-Host "❌ Lỗi thêm nhiều pin: $($_.Exception.Message)" -ForegroundColor Red
}

# STEP 6: Xem pin trong trạm
Write-Host "`n=== STEP 6: Xem pin trong trạm ===" -ForegroundColor Green
try {
    $stationBatteries = Invoke-RestMethod -Uri "$baseUrl/BatteryUnits/station/$stationId" -Method GET -Headers $headers
    Write-Host "✅ Lấy pin trong trạm thành công!" -ForegroundColor Green
    Write-Host "Tổng số pin: $($stationBatteries.data.Count)" -ForegroundColor Yellow
    
    # Hiển thị danh sách pin
    foreach ($battery in $stationBatteries.data) {
        Write-Host "  - $($battery.serial) | $($battery.status) | Model: $($battery.batteryModelName)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "❌ Lỗi lấy pin trong trạm: $($_.Exception.Message)" -ForegroundColor Red
}

# STEP 7: Xem thống kê pin
Write-Host "`n=== STEP 7: Xem thống kê pin trong trạm ===" -ForegroundColor Green
try {
    $stats = Invoke-RestMethod -Uri "$baseUrl/v1/Stations/$stationId/battery-stats" -Method GET -Headers $headers
    Write-Host "✅ Lấy thống kê thành công!" -ForegroundColor Green
    Write-Host "Tổng pin: $($stats.totalBatteries)" -ForegroundColor Yellow
    Write-Host "Pin sẵn sàng: $($stats.availableBatteries)" -ForegroundColor Yellow
    
    Write-Host "Phân bố theo trạng thái:" -ForegroundColor Cyan
    foreach ($stat in $stats.batteryStatusBreakdown) {
        Write-Host "  - $($stat.status): $($stat.count)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "❌ Lỗi lấy thống kê: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🎉 Test hoàn thành!" -ForegroundColor Green
Write-Host "💡 Tip: Mở Swagger UI tại http://localhost:5194/swagger để test thêm" -ForegroundColor Blue