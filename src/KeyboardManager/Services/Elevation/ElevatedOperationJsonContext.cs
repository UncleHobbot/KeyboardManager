using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeyboardManager.Services.Elevation;

[JsonSerializable(typeof(ElevatedOperation[]))]
[JsonSerializable(typeof(ElevatedResult))]
internal sealed partial class ElevatedOperationJsonContext : JsonSerializerContext
{
}
