using System;
using System.IO;
using Microsoft.AspNetCore.Http;

using Tlabs.IO;

namespace Tlabs.Server.Controller {

  ///<summary><see cref="HttpResponse"/> extension for writing schema data to the request response.</summary>
  public static class FormResponseExtension {
    const string STYLE_DATA= "styles.css";

    ///<summary>Write schema data to the request response.</summary>
    public static void WriteSchemaData(this HttpResponse resp, string schema, string form, Func<string, Stream> formDataStream, Func<string, Stream> styleDataStream) {
      if (string.IsNullOrEmpty(schema)) throw new ArgumentNullException(nameof(schema));

      if (string.Equals(STYLE_DATA, schema, StringComparison.OrdinalIgnoreCase)) {
        if (string.IsNullOrEmpty(form)) throw new ArgumentNullException(nameof(form));
        resp.ContentType= "text/css; charset=utf-8";
        styleDataStream(form).CopyTo(resp.Body);
        return;
      }

      resp.ContentType= "text/html; charset=utf-8";
      formDataStream(schema).CopyTo(resp.Body);
    }
  }

  ///<summary><see cref="IFormFile"/> extension for reading possibly base64 encoded binary data from a posted file.</summary>
  public static class FormFileExtension {
    // const string STYLE_DATA= "styles.css";

    ///<summary><see cref="IFormFile.OpenReadStream()"/> variation to handle base64 decoding.</summary>
    public static Stream OpenBase64DecodedReadStream(this IFormFile file) {
      ArgumentNullException.ThrowIfNull(file);

      //check for possible base64 encoding
      if (file.Headers["Content-Transfer-Encoding"].ToString().Equals("base64", StringComparison.OrdinalIgnoreCase))
#if DECODE_IN_MEMORY
      using (var rd = new StreamReader(file.OpenReadStream()))
        return new MemoryStream(Convert.FromBase64String(rd.ReadToEnd()));
#else
      return Base64Stream.CreateForDecoding(file.OpenReadStream());
#endif
      return file.OpenReadStream();
    }
  }
}