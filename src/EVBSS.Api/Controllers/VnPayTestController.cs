using EVBSS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EVBSS.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class VnPayTestController : ControllerBase
{
    private readonly IVnPayServiceV2 _vnPayService;

    public VnPayTestController(IVnPayServiceV2 vnPayService)
    {
        _vnPayService = vnPayService;
    }

    /// <summary>
    /// Tạo payment URL theo hướng dẫn VNPay chính thức
    /// </summary>
    [HttpPost("create-payment")]
    public IActionResult CreatePaymentUrlVnpay([FromBody] PaymentInformationModel model)
    {
        var url = _vnPayService.CreatePaymentUrl(model, HttpContext);

        return Ok(new
        {
            success = true,
            paymentUrl = url,
            message = "Tạo URL thanh toán thành công"
        });
    }

    /// <summary>
    /// Callback từ VNPay sau khi user thanh toán xong
    /// VNPay sẽ redirect user về URL này
    /// </summary>
    [HttpGet("payment-callback")]
    public IActionResult PaymentCallbackVnpay()
    {
        var response = _vnPayService.PaymentExecute(Request.Query);

        return Ok(response);
    }
}
