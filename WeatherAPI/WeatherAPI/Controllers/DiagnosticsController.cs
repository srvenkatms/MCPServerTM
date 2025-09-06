using Microsoft.AspNetCore.Mvc;
using WeatherAPI.Services;

namespace WeatherAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DiagnosticsController : ControllerBase
{
    private readonly IConfigurationValidationService _configValidation;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        IConfigurationValidationService configValidation,
        ILogger<DiagnosticsController> logger)
    {
        _configValidation = configValidation;
        _logger = logger;
    }

    /// <summary>
    /// Get configuration status and validation results
    /// </summary>
    /// <returns>Configuration status information</returns>
    [HttpGet("config")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> GetConfigurationStatus()
    {
        try
        {
            var isValid = await _configValidation.ValidateConfigurationAsync();
            var status = await _configValidation.GetConfigurationStatusAsync();

            var result = new
            {
                IsValid = isValid,
                ValidationTimestamp = DateTime.UtcNow,
                Configuration = status
            };

            if (!isValid)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting configuration status");
            return Problem("Failed to retrieve configuration status", statusCode: 500);
        }
    }

    /// <summary>
    /// Test OAuth token acquisition (without making actual API calls)
    /// </summary>
    /// <returns>OAuth configuration test results</returns>
    [HttpGet("test-oauth")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<object>> TestOAuthConfiguration()
    {
        try
        {
            var configStatus = await _configValidation.GetConfigurationStatusAsync();
            var mcpConfig = configStatus["McpServer"] as dynamic;

            var testResult = new
            {
                TestTimestamp = DateTime.UtcNow,
                TokenEndpoint = mcpConfig?.TokenEndpointResolved,
                ConfigurationValid = await _configValidation.ValidateConfigurationAsync(),
                Message = "OAuth configuration test completed. Check logs for detailed results when making actual token requests."
            };

            return Ok(testResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing OAuth configuration");
            return Problem("Failed to test OAuth configuration", statusCode: 500);
        }
    }

    /// <summary>
    /// Get application health and status
    /// </summary>
    /// <returns>Application health information</returns>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public ActionResult<object> GetHealth()
    {
        return Ok(new
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Weather API",
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            Endpoints = new
            {
                ConfigurationStatus = "/api/diagnostics/config",
                OAuthTest = "/api/diagnostics/test-oauth",
                Health = "/api/diagnostics/health"
            }
        });
    }
}