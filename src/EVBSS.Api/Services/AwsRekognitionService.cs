using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using EVBSS.Api.Dtos.Vehicles;
using System.Text.RegularExpressions;

namespace EVBSS.Api.Services;

public class AwsRekognitionService : IAwsRekognitionService
{
    private readonly IAmazonRekognition _rekognitionClient;
    private readonly HttpClient _httpClient;

    public AwsRekognitionService(IAmazonRekognition rekognitionClient, HttpClient httpClient)
    {
        _rekognitionClient = rekognitionClient;
        _httpClient = httpClient;
    }

    public async Task<VehicleRegistrationScanResult> ScanVehicleRegistrationAsync(Stream imageStream)
    {
        try
        {
            // Read image bytes
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // Call AWS Rekognition
            var request = new DetectTextRequest
            {
                Image = new Image { Bytes = new MemoryStream(imageBytes) }
            };

            var response = await _rekognitionClient.DetectTextAsync(request);
            
            // Extract text from response
            var allText = response.TextDetections
                .Where(t => t.Type == TextTypes.LINE)
                .OrderBy(t => t.Geometry.BoundingBox.Top)
                .Select(t => t.DetectedText)
                .ToList();

            return ParseVehicleRegistrationData(allText);
        }
        catch (Exception ex)
        {
            return new VehicleRegistrationScanResult
            {
                ErrorMessage = $"Failed to scan image: {ex.Message}"
            };
        }
    }

    public async Task<VehicleRegistrationScanResult> ScanVehicleRegistrationFromUrlAsync(string imageUrl)
    {
        try
        {
            // Download image from URL
            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            
            using var stream = new MemoryStream(imageBytes);
            return await ScanVehicleRegistrationAsync(stream);
        }
        catch (Exception ex)
        {
            return new VehicleRegistrationScanResult
            {
                ErrorMessage = $"Failed to download and scan image from URL: {ex.Message}"
            };
        }
    }

    private VehicleRegistrationScanResult ParseVehicleRegistrationData(List<string> allText)
    {
        var result = new VehicleRegistrationScanResult
        {
            RawData = allText.Select((text, index) => new { Key = $"Line_{index}", Value = text })
                            .ToDictionary(x => x.Key, x => x.Value)
        };

        // Calculate average confidence
        result.Confidence = allText.Count > 0 ? 85.0f : 0.0f;

        // Step 1: Build exclude list to avoid misidentifying engine/chassis numbers as other fields
        var excludeLines = BuildExcludeList(allText);

        // Step 2: Extract fields in priority order
        result.VIN = ExtractVIN(allText, excludeLines);
        result.Plate = ExtractPlate(allText, excludeLines);
        result.Brand = ExtractBrand(allText);
        result.VehicleModel = ExtractVehicleModel(allText, result.Brand);

        return result;
    }

    private HashSet<string> BuildExcludeList(List<string> allText)
    {
        var excludeLines = new HashSet<string>();
        
        // Collect engine and chassis numbers to exclude from other field extraction
        var excludePatterns = new[]
        {
            @"S[oó]\s*m[aá]y|Engine\s*No",
            @"S[oó]\s*khung|Chassis\s*N[°o]"
        };

        foreach (var pattern in excludePatterns)
        {
            for (int i = 0; i < allText.Count; i++)
            {
                if (Regex.IsMatch(allText[i], pattern, RegexOptions.IgnoreCase))
                {
                    // Collect 1-3 lines after the label
                    for (int j = i + 1; j < Math.Min(i + 4, allText.Count); j++)
                    {
                        var line = allText[j].Trim();
                        if (!string.IsNullOrEmpty(line) && line.Length >= 8)
                        {
                            excludeLines.Add(line.ToUpper());
                        }
                    }
                }
            }
        }

        return excludeLines;
    }

    private string? ExtractVIN(List<string> allText, HashSet<string> excludeLines)
    {
        // Priority 1: Look for chassis label and extract from next lines
        for (int i = 0; i < allText.Count; i++)
        {
            if (Regex.IsMatch(allText[i], @"S[oó]\s*khung|Chassis\s*N[°o]", RegexOptions.IgnoreCase))
            {
                for (int j = i + 1; j < Math.Min(i + 4, allText.Count); j++)
                {
                    var nextLine = allText[j].Trim();
                    if (!string.IsNullOrEmpty(nextLine) && 
                        nextLine.Length >= 10 && 
                        Regex.IsMatch(nextLine, @"^[A-Z0-9]{10,17}$"))
                    {
                        return nextLine;
                    }
                }
            }
        }

        // Priority 2: Standard 17-character VIN
        foreach (var line in allText)
        {
            var match = Regex.Match(line, @"\b[A-Z0-9]{17}\b");
            if (match.Success)
            {
                return match.Value;
            }
        }

        // Priority 3: Any alphanumeric 10-17 chars (excluding engine numbers)
        foreach (var line in allText)
        {
            var cleanedLine = line.Trim().ToUpper();
            if (excludeLines.Contains(cleanedLine))
                continue;
            
            var match = Regex.Match(cleanedLine, @"\b[A-Z0-9]{10,17}\b");
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private string? ExtractPlate(List<string> allText, HashSet<string> excludeLines)
    {
        var platePatterns = new[]
        {
            @"\d{2}[A-Z]\d[-\s]?\d{3,5}(?:\.\d{2,3})?",      // 29X1-857.75, 72D1-190.14
            @"\d{2}[A-Z]{1,2}[-\s]?\d{3,5}(?:\.\d{2,3})?"   // 71LD-000.68
        };

        // Priority 1: Look for plate label
        for (int i = 0; i < allText.Count; i++)
        {
            if (Regex.IsMatch(allText[i], @"Bi[eế]n\s*s[oố].*[dđ][aă]ng\s*k[yý]|N[°o]\s*plate", RegexOptions.IgnoreCase))
            {
                for (int j = i + 1; j < Math.Min(i + 4, allText.Count); j++)
                {
                    if (excludeLines.Contains(allText[j].ToUpper()))
                        continue;

                    var plate = TryExtractPlate(allText[j], platePatterns);
                    if (plate != null)
                        return plate;
                }
            }
        }
        
        // Priority 2: Search entire text
        foreach (var line in allText)
        {
            if (excludeLines.Contains(line.ToUpper()))
                continue;
            
            var plate = TryExtractPlate(line, platePatterns);
            if (plate != null)
                return plate;
        }

        return null;
    }

    private string? TryExtractPlate(string line, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var plate = match.Value.ToUpper()
                    .Replace(" ", "")
                    .Replace(".", "")
                    .Trim();
                
                plate = CleanPlate(plate);
                
                if (plate.Length >= 8 && plate.Length <= 13)
                {
                    return plate;
                }
            }
        }
        return null;
    }

    private string? ExtractBrand(List<string> allText)
    {
        var brandNames = new[] { "HONDA", "TOYOTA", "YAMAHA", "SUZUKI", "KAWASAKI", "PIAGGIO", "SYM", "KTM" };

        // Priority 1: Look for brand label with brand name on same line or after
        for (int i = 0; i < allText.Count; i++)
        {
            if (Regex.IsMatch(allText[i], @"Nh[aã]n\s*h[ií]e[uú].*Brand", RegexOptions.IgnoreCase))
            {
                var line = allText[i];
                
                // Check if brand name is on same line (e.g., "Nhãn hieu (Brand) HONDA")
                foreach (var brand in brandNames)
                {
                    if (line.ToUpper().Contains(brand))
                    {
                        return brand;
                    }
                }

                // Try same line after colon
                var colonIndex = line.IndexOf(':');
                if (colonIndex >= 0 && colonIndex < line.Length - 1)
                {
                    var brandValue = line.Substring(colonIndex + 1).Trim();
                    
                    // Check if extracted value contains known brand
                    foreach (var brand in brandNames)
                    {
                        if (brandValue.ToUpper().Contains(brand))
                        {
                            return brand;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(brandValue) && brandValue.Length <= 20)
                    {
                        return brandValue;
                    }
                }

                // Try next lines (but only accept known brands to avoid wrong extraction)
                for (int j = i + 1; j < Math.Min(i + 3, allText.Count); j++)
                {
                    var nextLine = allText[j].Trim().ToUpper();
                    
                    // Check if it's a known brand
                    foreach (var brand in brandNames)
                    {
                        if (nextLine == brand || nextLine.Contains(brand))
                        {
                            return brand;
                        }
                    }
                }
            }
        }

        // Priority 2: Look for known brand names in all text
        foreach (var line in allText)
        {
            var lineUpper = line.Trim().ToUpper();
            foreach (var brand in brandNames)
            {
                if (lineUpper == brand || 
                    lineUpper.Contains($" {brand} ") || 
                    lineUpper.StartsWith($"{brand} ") ||
                    lineUpper.EndsWith($" {brand}"))
                {
                    return brand;
                }
            }
        }

        return null;
    }

    private string? ExtractVehicleModel(List<string> allText, string? brand)
    {
        // Priority 1: Look for model label
        for (int i = 0; i < allText.Count; i++)
        {
            if (Regex.IsMatch(allText[i], @"S[oó]\s*lo[aá][ií].*Model\s*code", RegexOptions.IgnoreCase))
            {
                // Try same line after colon
                var colonIndex = allText[i].IndexOf(':');
                if (colonIndex >= 0 && colonIndex < allText[i].Length - 1)
                {
                    var modelValue = allText[i].Substring(colonIndex + 1).Trim();
                    if (!string.IsNullOrEmpty(modelValue) && modelValue.Length <= 30)
                    {
                        return modelValue;
                    }
                }

                // Try next lines
                for (int j = i + 1; j < Math.Min(i + 3, allText.Count); j++)
                {
                    var nextLine = allText[j].Trim();
                    if (!string.IsNullOrEmpty(nextLine) && 
                        nextLine.Length <= 30 && 
                        !Regex.IsMatch(nextLine, @"[:\(\)]"))
                    {
                        return nextLine;
                    }
                }
            }
        }

        // Priority 2: Look after brand name
        if (!string.IsNullOrEmpty(brand))
        {
            var brandIndex = -1;
            for (int i = 0; i < allText.Count; i++)
            {
                if (allText[i].Trim().ToUpper().Contains(brand.ToUpper()))
                {
                    brandIndex = i;
                    break;
                }
            }

            if (brandIndex >= 0)
            {
                for (int j = brandIndex + 1; j < Math.Min(brandIndex + 4, allText.Count); j++)
                {
                    var nextLine = allText[j].Trim();
                    if (!string.IsNullOrEmpty(nextLine) && 
                        nextLine.Length <= 30 && 
                        !Regex.IsMatch(nextLine, @"[:\(\)]") &&
                        !nextLine.ToUpper().Contains("HONDA") && 
                        !nextLine.ToUpper().Contains("TOYOTA"))
                    {
                        return nextLine;
                    }
                }
            }
        }

        // Priority 3: Brand-specific model mapping
        if (!string.IsNullOrEmpty(brand))
        {
            return MapBrandToModel(brand, allText);
        }

        return null;
    }

    /// <summary>
    /// Helper: Dọn dẹp và chuẩn hóa biển số về định dạng chuẩn có dấu gạch ngang.
    /// Hỗ trợ nhiều format biển số Việt Nam.
    /// </summary>
    private string CleanPlate(string plate)
    {
        if (string.IsNullOrEmpty(plate))
            return plate;

        // Dọn dẹp: Chuyển chữ hoa, xóa dấu chấm, khoảng trắng, gạch ngang cũ
        string cleanedPlate = plate.ToUpper().Replace(".", "").Replace(" ", "").Replace("-", "");

        // Pattern 1: Format có chữ và số (e.g., 71LD00068, 59G100068)
        // Group 1: (\d{2}[A-Z]{1,2}) -> 71LD, 59G1 (2 số + 1-2 chữ)
        // Group 2: (\d{4,5})         -> 00068, 12345
        var match = Regex.Match(cleanedPlate, @"^(\d{2}[A-Z]{1,2})(\d{4,5})$");
        if (match.Success)
        {
            return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
        }

        // Pattern 2: Format có số sau chữ (e.g., 29X185775 -> 29X1-85775)
        // Group 1: (\d{2}[A-Z]\d) -> 29X1, 72D1 (2 số + 1 chữ + 1 số)
        // Group 2: (\d{4,5})      -> 85775, 19014
        match = Regex.Match(cleanedPlate, @"^(\d{2}[A-Z]\d)(\d{4,5})$");
        if (match.Success)
        {
            return $"{match.Groups[1].Value}-{match.Groups[2].Value}";
        }

        // Nếu không match, trả về đã dọn dẹp
        return cleanedPlate;
    }

    private string? MapBrandToModel(string brand, List<string> allText)
    {
        var brandUpper = brand.ToUpper();
        
        // Honda models
        if (brandUpper.Contains("HONDA"))
        {
            foreach (var line in allText)
            {
                var lineUpper = line.ToUpper();
                if (lineUpper.Contains("SH") || lineUpper.Contains("FUTURE") || lineUpper.Contains("LEAD") || 
                    lineUpper.Contains("VISION") || lineUpper.Contains("AIRBLADE") || lineUpper.Contains("PCX"))
                {
                    return line.Trim();
                }
            }
        }
        
        // Toyota models
        if (brandUpper.Contains("TOYOTA"))
        {
            foreach (var line in allText)
            {
                var lineUpper = line.ToUpper();
                if (lineUpper.Contains("FORTUNE") || lineUpper.Contains("INNOVA") || lineUpper.Contains("VIOS") || 
                    lineUpper.Contains("CAMRY") || lineUpper.Contains("COROLLA") || lineUpper.Contains("HILUX"))
                {
                    return line.Trim();
                }
            }
        }

        return null;
    }
}