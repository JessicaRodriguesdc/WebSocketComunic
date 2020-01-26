using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebSocketComunic
{
    public static class WebSocketServer
    {

        private static HttpListener Listener;

        private static CancellationTokenSource SocketLoopTokenSource;
        private static CancellationTokenSource ListenerLoopTokenSource;

        private static int SocketCounter = 0;

        private static bool ServerIsRunning = true;

        // The key is a socket id
        private static ConcurrentDictionary<int, ConnectedClient> Clients = new ConcurrentDictionary<int, ConnectedClient>();

        public static void Start(string uriPrefix)
        {
            SocketLoopTokenSource = new CancellationTokenSource();
            ListenerLoopTokenSource = new CancellationTokenSource();
            Listener = new HttpListener();
            Listener.Prefixes.Add(uriPrefix);
            Listener.Start();
            if (Listener.IsListening)
            {
                Console.WriteLine("Conecte o navegador para obter uma página da web básica de retorno.");
                Console.WriteLine($"Server listening: {uriPrefix}");
                // listen on a separate thread so that Listener.Stop can interrupt GetContextAsync
                Task.Run(() => ListenerProcessingLoopAsync().ConfigureAwait(false));
            }
            else
            {
                Console.WriteLine("Server failed to start.");
            }
        }

        public static async Task StopAsync()
        {
            if (Listener?.IsListening ?? false && ServerIsRunning)
            {
                Console.WriteLine("\nO servidor está parando.");

                ServerIsRunning = false;            // impedir novas conexões durante o desligamento
                await CloseAllSocketsAsync();            // também cancela os tokens de loop de processamento (abort ReceiveAsync)
                ListenerLoopTokenSource.Cancel();   // seguro parar agora que os sockets  estão fechados
                Listener.Stop();
                Listener.Close();
            }
        }

        public static void Servidor(string message)
        {
            Console.WriteLine($"Servidor: {message}");
            foreach(var kvp in Clients)
                kvp.Value.BroadcastQueue.Add(message);
        }

        private static async Task ListenerProcessingLoopAsync()
        {
            var cancellationToken = ListenerLoopTokenSource.Token;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    HttpListenerContext context = await Listener.GetContextAsync();
                    if (ServerIsRunning)
                    {
                        if (context.Request.IsWebSocketRequest)
                        {
                            // HTTP is only the initial connection; upgrade to a client-specific websocket
                            HttpListenerWebSocketContext wsContext = null;
                            try
                            {
                                wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                                int socketId = Interlocked.Increment(ref SocketCounter);
                                var client = new ConnectedClient(socketId, wsContext.WebSocket);
                                Clients.TryAdd(socketId, client);
                                Console.WriteLine($"Socket {socketId}: New connection.");
                                _ = Task.Run(() => SocketProcessingLoopAsync(client).ConfigureAwait(false));
                            }
                            catch (Exception)
                            {
                                // server error if upgrade from HTTP to WebSocket fails
                                context.Response.StatusCode = 500;
                                context.Response.StatusDescription = "WebSocket upgrade failed";
                                context.Response.Close();
                                return;
                            }
                        }
                        else
                        {
                            if (context.Request.AcceptTypes.Contains("text/html"))
                            {
                                Console.WriteLine("Sending HTML to client.");
                                ReadOnlyMemory<byte> HtmlPage = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(SimpleHtmlClient.HTML));
                                context.Response.ContentType = "text/html; charset=utf-8";
                                context.Response.StatusCode = 200;
                                context.Response.StatusDescription = "OK";
                                context.Response.ContentLength64 = HtmlPage.Length;
                                await context.Response.OutputStream.WriteAsync(HtmlPage, CancellationToken.None);
                                await context.Response.OutputStream.FlushAsync(CancellationToken.None);
                            }
                            else
                            {
                                context.Response.StatusCode = 400;
                            }
                            context.Response.Close();
                        }
                    }
                    else
                    {
                        // HTTP 409 Conflict (with server's current state)
                        context.Response.StatusCode = 409;
                        context.Response.StatusDescription = "Server is shutting down";
                        context.Response.Close();
                        return;
                    }
                }
            }
            catch (HttpListenerException ex) when (ServerIsRunning)
            {
                Program.ReportException(ex);
            }
        }

        private static async Task SocketProcessingLoopAsync(ConnectedClient client)
        {
            _ = Task.Run(() => client.LoopAsync().ConfigureAwait(false));

            var socket = client.Socket;
            var loopToken = SocketLoopTokenSource.Token;
            var broadcastTokenSource = client.LoopTokenSource; // armazenar uma cópia para uso no bloco finalmente
            try
            {
                var buffer = WebSocket.CreateServerBuffer(4096);
                while (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted && !loopToken.IsCancellationRequested)
                {
                    var receiveResult = await client.Socket.ReceiveAsync(buffer, loopToken);
                    // se o token for cancelado enquanto o ReceiveAsync estiver bloqueando, o estado do soquete mudará para abortado e não poderá ser usado
                    if (!loopToken.IsCancellationRequested)
                    {
                        // o cliente está nos notificando que a conexão será fechada; enviar confirmação
                        if (client.Socket.State == WebSocketState.CloseReceived && receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            Console.WriteLine($"Socket {client.SocketId}: Confirmando Fechar quadro recebido do cliente ");
                            broadcastTokenSource.Cancel();
                            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Confirmar quadro fechado", CancellationToken.None);
                            // o estado do soquete muda para fechado neste momento
                        }

                        // echo text ou dados binários para a fila de transmissão
                        if (client.Socket.State == WebSocketState.Open)
                        {
                            Console.WriteLine($"Socket {client.SocketId}: Received {receiveResult.MessageType} frame ({receiveResult.Count} bytes).");
                            Console.WriteLine($"Socket {client.SocketId}: Echoing data to queue.");
                            string message = Encoding.UTF8.GetString(buffer.Array, 0, receiveResult.Count);
                            client.BroadcastQueue.Add(message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal mediante cancelamento de tarefa / token, desconsidera
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Socket {client.SocketId}:");
                Program.ReportException(ex);
            }
            finally
            {
                broadcastTokenSource.Cancel();

                Console.WriteLine($"Socket {client.SocketId}: Loop de processamento finalizado no estado {socket.State}");

                // não deixe o Socket em nenhum estado potencialmente conectado
                if (client.Socket.State != WebSocketState.Closed)
                    client.Socket.Abort();

                // nesse ponto, o Socket é fechado ou abortado, o objeto ConnectedClient é inútil
                if (Clients.TryRemove(client.SocketId, out _))
                    socket.Dispose();
            }
        }

        private static async Task CloseAllSocketsAsync()
        {
            //Não podemos descartar os soquetes até que os ciclos de processamento sejam finalizados,
            //mas encerrar os loops abortará os soquetes, impedindo o fechamento gracioso.
            var disposeQueue = new List<WebSocket>(Clients.Count);

            while (Clients.Count > 0)
            {
                var client = Clients.ElementAt(0).Value;
                Console.WriteLine($"Socket fechado{client.SocketId}");

                Console.WriteLine("... final do loop do servidor");
                client.LoopTokenSource.Cancel();

                if(client.Socket.State != WebSocketState.Open)
                {
                    Console.WriteLine($"... socket não aberto, Estado = {client.Socket.State}");
                }
                else
                {
                    var timeout = new CancellationTokenSource(Program.CLOSE_SOCKET_TIMEOUT_MS);
                    try
                    {
                        Console.WriteLine("... starting close handshake");
                        await client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fechado", timeout.Token);
                    }
                    catch (OperationCanceledException ex)
                    {
                        Program.ReportException(ex);
                        // normal mediante cancelamento de tarefa / token, desconsidera
                    }
                }

                if(Clients.TryRemove(client.SocketId, out _))
                {
                    // só é seguro Dispor uma vez, portanto, adicione-o apenas se esse loop não puder processá-lo novamente
                    disposeQueue.Add(client.Socket);
                }

                Console.WriteLine("... done");
            }

            // agora que todos estão fechados, encerre as chamadas ReceiveAsync de bloqueio nos threads SocketProcessingLoop
            SocketLoopTokenSource.Cancel();

            // dispor de todos os recursos
            foreach (var socket in disposeQueue)
                socket.Dispose();
        }

    }
}
