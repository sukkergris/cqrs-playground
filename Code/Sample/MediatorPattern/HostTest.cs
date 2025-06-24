#:package Microsoft.Extensions.Hosting@10.0.0-preview.5.25277.114
#:package Microsoft.Extensions.Hosting.Abstractions@10.0.0-preview.5.25277.114

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register the central service as a Singleton so everyone shares the same instance.
        services.AddSingleton<StatusService>(); 
        
        // Register the worker as a Hosted Service so the host starts and stops it.
        services.AddHostedService<DataProcessingService>();
        
        services.AddHostedService<MyCronJobService>(); 
    })
    .Build()
    .RunAsync();

// This is your active, running service
public class MyCronJobService : IHostedService, IDisposable
{
    private readonly ILogger<MyCronJobService> _logger;
    private Timer? _timer;

    public MyCronJobService(ILogger<MyCronJobService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cron Job Service is starting.");
        
        // Set up a timer to do work every 5 seconds
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        
        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        _logger.LogInformation("Cron Job is working. The time is {time}", DateTimeOffset.Now);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cron Job Service is stopping.");
        
        // Stop the timer
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

// No IHostedService needed. This is a passive service.
public class StatusService 
{
    private int _processedItems = 0;
    private readonly ILogger<StatusService> _logger;

    public StatusService(ILogger<StatusService> logger)
    {
        _logger = logger;
    }

    public void ReportItemProcessed()
    {
        _processedItems++;
        _logger.LogInformation("Total items processed: {count}", _processedItems);
    }
}

public class DataProcessingService : IHostedService, IDisposable
{
    private readonly ILogger<DataProcessingService> _logger;
    private readonly StatusService _statusService; // <-- Inject the other service!
    private Timer? _timer;

    // The DI container provides the instance of StatusService here
    public DataProcessingService(ILogger<DataProcessingService> logger, StatusService statusService)
    {
        _logger = logger;
        _statusService = statusService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(ProcessData, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        return Task.CompletedTask;
    }
    
    private void ProcessData(object? state)
    {
        _logger.LogInformation("Processing a new item...");
        // This is the communication!
        _statusService.ReportItemProcessed(); 
    }
    
    // ... StopAsync and Dispose implementation ...
    public Task StopAsync(CancellationToken stoppingToken) { _timer?.Change(Timeout.Infinite, 0); return Task.CompletedTask; }
    public void Dispose() { _timer?.Dispose(); }
}