using System.Diagnostics;

namespace DisableWakeArmedDevices.WorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private const int SleepTimeInMinutes = 1;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                await RunJobAsync();

                await Task.Delay(TimeSpan.FromMinutes(SleepTimeInMinutes), stoppingToken);
            }
        }

        private async Task RunJobAsync()
        {
            // Start a powershell process
            var processInfo = new ProcessStartInfo()
            {
                FileName = "powercfg.exe",
                Arguments = @"/devicequery wake_armed",
                RedirectStandardOutput = true
            };

            using var process = new Process
            {
                StartInfo = processInfo
            };

            // Read the process's StandardOutput and log the data as string

            process.Start();
            await process.WaitForExitAsync();
            
            var lines = (await process.StandardOutput.ReadToEndAsync())
                .Split(Environment.NewLine)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim());

            if (!lines.Any()
                || lines.First() == "NONE") // No Wake Armed Devices
                return;

            foreach (var wakeArmedDevice in lines)
            {
                var disableProcessInfo = new ProcessStartInfo()
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/devicedisablewake \"{wakeArmedDevice}\"",
                };

                using var disableProcess = new Process
                {
                    StartInfo = disableProcessInfo,
                };

                disableProcess.Start();
                await disableProcess.WaitForExitAsync();

                if (disableProcess.ExitCode == 0)
                {
                    _logger.LogInformation("Disabled: {wakeArmedDevice}", wakeArmedDevice);
                }
                else
                {
                    _logger.LogError("Failed to disable: {wakeArmedDevice}", wakeArmedDevice);
                }
            }
        }
    }
}