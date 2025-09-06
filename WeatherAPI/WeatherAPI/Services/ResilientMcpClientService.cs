using WeatherAPI.Models;

namespace WeatherAPI.Services;

public class ResilientMcpClientService : IMcpClientService
{
    private readonly IMcpClientService _innerService;
    private readonly RetryConfig _retryConfig;
    private readonly ILogger<ResilientMcpClientService> _logger;

    public ResilientMcpClientService(
        IMcpClientService innerService,
        RetryConfig retryConfig,
        ILogger<ResilientMcpClientService> logger)
    {
        _innerService = innerService;
        _retryConfig = retryConfig;
        _logger = logger;
    }

    public async Task<CurrentWeatherInfo?> GetCurrentWeatherAsync(string state, string? city = null)
    {
        return await ExecuteWithRetryAsync(
            () => _innerService.GetCurrentWeatherAsync(state, city),
            $"GetCurrentWeatherAsync(state: {state}, city: {city})");
    }

    public async Task<WeatherForecastInfo[]?> GetWeatherForecastAsync(string state, int days = 5)
    {
        return await ExecuteWithRetryAsync(
            () => _innerService.GetWeatherForecastAsync(state, days),
            $"GetWeatherForecastAsync(state: {state}, days: {days})");
    }

    public async Task<WeatherAlertInfo[]?> GetWeatherAlertsAsync(string state)
    {
        return await ExecuteWithRetryAsync(
            () => _innerService.GetWeatherAlertsAsync(state),
            $"GetWeatherAlertsAsync(state: {state})");
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= _retryConfig.MaxRetries)
        {
            try
            {
                _logger.LogDebug("Executing {Operation}, attempt {Attempt}", operationName, attempt + 1);
                return await operation();
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                attempt++;
                
                if (attempt <= _retryConfig.MaxRetries)
                {
                    var delay = CalculateDelay(attempt);
                    _logger.LogWarning(ex, "Network error during {Operation}, attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms", 
                        operationName, attempt, _retryConfig.MaxRetries + 1, delay);
                    await Task.Delay(delay);
                }
                else
                {
                    _logger.LogError(ex, "All retry attempts failed for {Operation}", operationName);
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                lastException = ex;
                attempt++;
                
                if (attempt <= _retryConfig.MaxRetries)
                {
                    var delay = CalculateDelay(attempt);
                    _logger.LogWarning(ex, "Timeout during {Operation}, attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms", 
                        operationName, attempt, _retryConfig.MaxRetries + 1, delay);
                    await Task.Delay(delay);
                }
                else
                {
                    _logger.LogError(ex, "All retry attempts failed for {Operation} due to timeouts", operationName);
                }
            }
            catch (Exception ex)
            {
                // Don't retry for non-transient errors like JSON parsing, authentication, etc.
                _logger.LogError(ex, "Non-retryable error during {Operation}", operationName);
                throw;
            }
        }

        // If we get here, all retries failed
        if (lastException != null)
        {
            throw lastException;
        }

        return default(T)!; // This should never be reached
    }

    private int CalculateDelay(int attempt)
    {
        if (!_retryConfig.UseExponentialBackoff)
        {
            return _retryConfig.DelayMilliseconds;
        }

        // Exponential backoff with jitter
        var baseDelay = _retryConfig.DelayMilliseconds;
        var exponentialDelay = baseDelay * Math.Pow(2, attempt - 1);
        
        // Add some jitter to prevent thundering herd
        var random = new Random();
        var jitter = random.Next(0, (int)(exponentialDelay * 0.1));
        
        return (int)Math.Min(exponentialDelay + jitter, 30000); // Cap at 30 seconds
    }
}