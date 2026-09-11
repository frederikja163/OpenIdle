using System;
using System.Text;
using Backend.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers.Http;

[ApiController]
public sealed class SchemaController : ControllerBase
{
    private static readonly string Json =
        Encoding.UTF8.GetString(Convert.FromBase64String(ProtocolSchemaJson.Base64));

    [HttpGet("/schema")]
    public IActionResult Schema() => Content(Json, "application/json");
}
