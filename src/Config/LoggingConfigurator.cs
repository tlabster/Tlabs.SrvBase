using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Tlabs.Config {
  ///<summary>Configures a console logger.</summary>
  public class SysLoggingConfigurator : IConfigurator<ILoggingBuilder> {
    ///<inherit/>
    public void AddTo(ILoggingBuilder log, IConfiguration cfg) {
      var optConfig= cfg.GetSection("options");
      log.Services.Configure<ConsoleFormatterOptions>(optConfig);
    
     log.AddSystemdConsole();
    }
  }

  ///<summary>Configures a Serilog file logger.</summary>
  public class FileLoggingConfigurator : IConfigurator<ILoggingBuilder> {
    ///<inherit/>
    public void AddTo(ILoggingBuilder log, IConfiguration cfg) {
      Environment.SetEnvironmentVariable("EXEPATH", Path.GetDirectoryName(App.MainEntryPath));
      var optConfig= cfg.GetSection("options");
      log.AddFile(optConfig);
      // App.AppLifetime.ApplicationStopped.Register(()=> Serilog.Log.CloseAndFlush());
    }
  }

  ///<summary>Configures a console logger.</summary>
  public class StdoutLoggingConfigurator : IConfigurator<ILoggingBuilder> {
    ///<inherit/>
    public void AddTo(ILoggingBuilder log, IConfiguration cfg) {
      var optConfig= cfg.GetSection("options");
      log.Services.Configure<CustomStdoutFormatterOptions>(optConfig);

      log.AddConsole(opt => opt.FormatterName= CustomStdoutFormatter.NAME)
         .AddConsoleFormatter<CustomStdoutFormatter, CustomStdoutFormatterOptions>();
    }
  }

  ///<summary>Custom stdout formatter.</summary>
  public sealed class CustomStdoutFormatterOptions : ConsoleFormatterOptions {
    ///<summary>Default ctor.</summary>
    public CustomStdoutFormatterOptions() {
      this.TimestampFormat= "O";
    }
  }
  ///<summary>Custom stdout formatter.</summary>
  ///<remarks>This custom formatter generates log entries with this format:
  ///<code>{timestamp} [{level}] {category}: {message?} {exception?}{newline}</code>
  ///</remarks>
  public sealed class CustomStdoutFormatter : ConsoleFormatter {
    ///<summary>Custom stdout formatter name.</summary>
    public const string NAME= "stdoutFormat";
    CustomStdoutFormatterOptions options;
    ///<summary>Ctor from <paramref name="opt"/>.</summary>
    public CustomStdoutFormatter(IOptions<CustomStdoutFormatterOptions> opt) : base(NAME) {
      this.options= opt.Value;
    }
    ///<inheritdoc/>
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter) {
      var msg= logEntry.Formatter(logEntry.State, logEntry.Exception);
      if (string.IsNullOrEmpty(msg) && null == logEntry.Exception) return;  //nothing to log

      if (!string.IsNullOrEmpty(options.TimestampFormat)) {
        textWriter.Write(App.TimeInfo.Now.ToString(options.TimestampFormat));
        textWriter.Write(' ');
      }
      textWriter.Write(logLevelMark(logEntry.LogLevel));
      
      textWriter.Write(logEntry.Category);
      textWriter.Write(": ");

      if (options.IncludeScopes && null != scopeProvider) {
        scopeProvider.ForEachScope((scope, state) => {
          state.Write("=> ");
          state.Write(scope);
          state.Write(' ');
        }, textWriter);
      }

      if (!string.IsNullOrEmpty(msg)) {
        textWriter.Write(msg);
        textWriter.Write(' ');
      }

      if (null != logEntry.Exception) {
        textWriter.Write(logEntry.Exception.ToString());
      }
      textWriter.WriteLine();
    }

    string logLevelMark(LogLevel lev) {
      return lev switch {
        LogLevel.Critical => "[CRT] ",
        LogLevel.Error => "[ERR] ",
        LogLevel.Warning => "[WRN] ",
        LogLevel.Information => "[INF] ",
        LogLevel.Debug => "[DBG] ",
        LogLevel.Trace => "[TRC] ",
        _ => "[???]"
      };
    }
  }

}