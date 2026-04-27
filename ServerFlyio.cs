using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

public interface IMessageHandler
{
    void HandleMessage(string message);
}

public interface IChatServer
{
    Task StartAsync();
}

public class UdpChatServer : IChatServer
{
    private readonly int port;
    private readonly IMessageHandler messageHandler;
    private UdpClient? server;
    private ConcurrentDictionary<IPEndPoint, ClientInfo> clients = new();
    private StringBuilder messageHistory = new();
    private int clientCounter = 0;

    private class ClientInfo
    {
        public string Nickname { get; set; } = "";
        public int ColorCode { get; set; }
        public int ClientNumber { get; set; }
    }

    public UdpChatServer(int port, IMessageHandler messageHandler)
    {
        this.port = port;
        this.messageHandler = messageHandler;
    }

    public async Task StartAsync()
    {
        InitializeServer();
        _ = Task.Run(ReceiveMessagesAsync);
        await Task.Delay(-1);
    }

    private void InitializeServer()
    {
        IPAddress listenAddress;

        if (Environment.GetEnvironmentVariable("FLY_APP_NAME") != null)
        {
            var flyUdpEndpoint = new IPEndPoint(Dns.GetHostEntry("fly-global-services").AddressList[0], port);
            server = new UdpClient(flyUdpEndpoint);
            messageHandler.HandleMessage($"сервер запущено на Fly.io (UDP порт {port}).");
        }
        else
        {
            server = new UdpClient(port);
            messageHandler.HandleMessage($"сервер запущено на порту {port} (локальний режим).");
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        while (true)
        {
            var result = await server.ReceiveAsync();
            var message = Encoding.UTF8.GetString(result.Buffer);
            var parts = message.Split('|');

            if (!clients.ContainsKey(result.RemoteEndPoint))
            {
                if (parts[0] == "NICK" && parts.Length >= 3)
                {
                    var clientInfo = new ClientInfo
                    {
                        Nickname = parts[1],
                        ColorCode = int.Parse(parts[2]),
                        ClientNumber = ++clientCounter
                    };
                    clients[result.RemoteEndPoint] = clientInfo;

                    await SendHistoryAsync(result.RemoteEndPoint);
                    messageHandler.HandleMessage($"\nклієнт підключено: {clientInfo.Nickname} (Клієнт #{clientCounter})");

                    var joinMessage = $"{GetTimestamp()}|{clientInfo.Nickname}|приєднався до чату|{clientInfo.ColorCode}";
                    messageHistory.AppendLine(joinMessage);
                    await BroadcastMessageAsync(joinMessage, result.RemoteEndPoint);
                }
                continue;
            }

            if (message == "EXIT")
            {
                var clientInfo = clients[result.RemoteEndPoint];
                clients.TryRemove(result.RemoteEndPoint, out _);
                messageHandler.HandleMessage($"\nклієнт відключено: {clientInfo.Nickname}");

                var leaveMessage = $"{GetTimestamp()}|{clientInfo.Nickname}|покинув чат|{clientInfo.ColorCode}";
                messageHistory.AppendLine(leaveMessage);
                await BroadcastMessageAsync(leaveMessage, result.RemoteEndPoint);
                continue;
            }

            if (parts[0] == "MSG" && parts.Length >= 2)
            {
                var clientInfo = clients[result.RemoteEndPoint];
                var formattedMessage = $"{GetTimestamp()}|{clientInfo.Nickname}|{parts[1]}|{clientInfo.ColorCode}";
                messageHandler.HandleMessage(formattedMessage);
                messageHistory.AppendLine(formattedMessage);
                await BroadcastMessageAsync(formattedMessage, result.RemoteEndPoint);
            }
        }
    }

    private string GetTimestamp()
    {
        return DateTime.Now.ToString("HH:mm:ss");
    }

    private async Task SendHistoryAsync(IPEndPoint client)
    {
        var history = Encoding.UTF8.GetBytes($"HISTORY|{messageHistory}");
        await server.SendAsync(history, history.Length, client);
    }

    private async Task BroadcastMessageAsync(string message, IPEndPoint excludedClient = null)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var client in clients.Keys)
        {
            if (!client.Equals(excludedClient))
                await server.SendAsync(data, data.Length, client);
        }
    }
}

public class ConsoleMessageHandler : IMessageHandler
{
    public void HandleMessage(string message)
    {
        Console.WriteLine(message);
    }
}

class ServerProgram
{
    static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "СЕРВЕРНА СТОРОНА";
        var handler = new ConsoleMessageHandler();
        var server = new UdpChatServer(9000, handler);
        await server.StartAsync();
    }
}
