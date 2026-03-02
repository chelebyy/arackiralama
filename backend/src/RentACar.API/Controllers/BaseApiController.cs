using Microsoft.AspNetCore.Mvc;
using RentACar.API.Contracts;

namespace RentACar.API.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult OkResponse<T>(T data, string message = "OK") =>
        Ok(ApiResponse<T>.Ok(data, message));

    protected IActionResult BadRequestResponse(string message) =>
        BadRequest(ApiResponse<object>.Fail(message));

    protected IActionResult UnauthorizedResponse(string message = "Yetkisiz erişim") =>
        Unauthorized(ApiResponse<object>.Fail(message));
}
