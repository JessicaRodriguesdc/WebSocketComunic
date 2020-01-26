using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// This differs from WebSocketExample in that this "outer" program can
// send messages to all open sockets by calling the new Broadcast method
// on the WebSocketServer class.

namespace WebSocketComunic
{
    public class Program
    {
        public const int TIMESTAMP_INTERVAL_SEC = 15;
        public const int SERVICE_TRANSMIT_INTERVAL_MS = 250;
        public const int CLOSE_SOCKET_TIMEOUT_MS = 2500;

        static async Task Main(string[] args)
        {
            try
            {
                WebSocketServer.Start("http://localhost:8080/");
                Console.WriteLine("Aperte qualquer tecla para sair...\n");

                DateTimeOffset nextMessage = DateTimeOffset.Now.AddSeconds(TIMESTAMP_INTERVAL_SEC);
                while(!Console.KeyAvailable)
                {
                    if(DateTimeOffset.Now > nextMessage)
                    {
                        nextMessage = DateTimeOffset.Now.AddSeconds(TIMESTAMP_INTERVAL_SEC);
                        WebSocketServer.Servidor($"Server time: {DateTimeOffset.Now.ToString("o")}");
                    }
                }

                await WebSocketServer.StopAsync();
            }
            catch (OperationCanceledException)
            {
                // normal após o cancelamento da tarefa / token, desconsidera
            }
            catch (Exception ex)
            {
                ReportException(ex);
            }

          
        }

        public static void ReportException(Exception ex, [CallerMemberName]string location = "(Nome do chamador não definido)")
        {
            Console.WriteLine($"\n{location}:\n  Exception {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"  Inner Exception {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
        }
    }
}
