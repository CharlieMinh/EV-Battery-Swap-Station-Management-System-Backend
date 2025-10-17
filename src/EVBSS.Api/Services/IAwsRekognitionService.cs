using EVBSS.Api.Dtos.Vehicles;

namespace EVBSS.Api.Services;

public interface IAwsRekognitionService
{
    Task<VehicleRegistrationScanResult> ScanVehicleRegistrationAsync(Stream imageStream);
    Task<VehicleRegistrationScanResult> ScanVehicleRegistrationFromUrlAsync(string imageUrl);
}
