using System.IO.Pipes;
using System.Text;

namespace IpcChannel;

internal static class NamedPipeServerStreamExtensions
{
    public static async Task<string> ReadMessage(this NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        var commandBuilder = new StringBuilder();
        do
        {
            var read = await pipe.ReadAsync(buffer, cancellationToken);
            commandBuilder.Append(Encoding.UTF8.GetString(buffer, 0, read));
        } while (!pipe.IsMessageComplete);

        return commandBuilder.ToString();
    }
}