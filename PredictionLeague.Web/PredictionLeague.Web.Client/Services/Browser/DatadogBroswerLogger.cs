namespace PredictionLeague.Web.Client.Services.Browser
{
    using Microsoft.Extensions.Logging;
    using Microsoft.JSInterop;

    public class DatadogBrowserLogger : ILogger
    {
        private readonly IJSRuntime _jsRuntime;

        public DatadogBrowserLogger(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public IDisposable BeginScope<TState>(TState state) => default;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var level = GetDatadogLogLevel(logLevel);

            _jsRuntime.InvokeVoidAsync("datadogLogs.logger.log", message, new { Level = level }, level);
        }

        private string GetDatadogLogLevel(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Trace => "debug",
                LogLevel.Debug => "debug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warn",
                LogLevel.Error => "error",
                LogLevel.Critical => "error",
                _ => "info"
            };
        }
    }
}
