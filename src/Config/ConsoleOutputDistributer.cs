using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tlabs.Config {
  /// <summary>Distributer of <see cref="Console"/> output.</summary>
  public class ConsoleOutputDistributer : TextWriter {
    readonly Dictionary<Stream, TextWriter> writers= new();
    /// <summary>Default ctor</summary>
    public ConsoleOutputDistributer() {
      writers[Stream.Null]= Console.Out;
      this.Encoding= Console.Out.Encoding;
      Console.SetOut(this);    //capture stdout
    }

    /// <summary>Add <paramref name="strm"/></summary>
    public void AddStream(Stream strm) {
      var wr= new StreamWriter(strm, this.Encoding, 512);
      wr.AutoFlush= true;
      lock (writers) writers[strm]= wr;
    }
    /// <summary>Remove <paramref name="strm"/></summary>
    public void RemoveStream(Stream strm) {
      lock (writers) writers.Remove(strm);
    }
    /// <inheritdoc/>
    public override Encoding Encoding { get; }
    /// <inheritdoc/>
    public override void Write(char c) {
      foreach (var wr in writers.Values) {
        wr.Write(c);
      }
    }
    /// <inheritdoc/>
    public override void Write(string? s) {
      foreach (var wr in writers.Values) {
        wr.Write(s);
      }
    }

    /// <summary>Configurator</summary>
    public class Configurator : IConfigurator<IServiceCollection> {
      ///<inheritdoc/>
      public void AddTo(IServiceCollection services, IConfiguration cfg) {
        services.AddSingleton<ConsoleOutputDistributer>();
      }
    }
  }
}