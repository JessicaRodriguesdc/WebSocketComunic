using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketComunic
{
    public class ConnectedClient
    {
        public ConnectedClient(int socketId, WebSocket socket)
        {
            SocketId = socketId;
            Socket = socket;
        }

         public int SocketId { get; private set; }

        //public string SocketId { get; private set; }

        public WebSocket Socket { get; private set; }

        public BlockingCollection<string> BroadcastQueue { get; } = new BlockingCollection<string>();

        public CancellationTokenSource LoopTokenSource { get; set; } = new CancellationTokenSource();

        public async Task LoopAsync()
        {
            var cancellationToken = LoopTokenSource.Token;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Program.SERVICE_TRANSMIT_INTERVAL_MS, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested && Socket.State == WebSocketState.Open && BroadcastQueue.TryTake(out var message))
                    {
                        Console.WriteLine($"Socket {SocketId}: Executando.");
                        var msgbuf = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                        await Socket.SendAsync(msgbuf, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);
                    }
                }
                catch (OperationCanceledException)
                {
                    // normal mediante cancelamento de tarefa / token, desconsidera
                }
                catch (Exception ex)
                {
                    Program.ReportException(ex);
                }
            }
        }

    }
}
