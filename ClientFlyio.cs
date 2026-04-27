using System.Net;
using System.Net.Sockets;
using System.Text;

public interface IMessageHandler
{
    void HandleMessage(string message);
}

public interface IChatClient
{
    Task ConnectAsync();
}

public class UdpChatClient : IChatClient
{
    private readonly int serverPort;
    private readonly string? serverIp;
    private readonly IMessageHandler? messageHandler;
    private UdpClient? client;
    private IPEndPoint? serverEndPoint;
    private string nickname = "";
    private int colorCode = 1;

    public UdpChatClient(string? serverIp, int serverPort, IMessageHandler? messageHandler, string nickname, int colorCode)
    {
        this.serverIp = serverIp;
        this.serverPort = serverPort;
        this.messageHandler = messageHandler;
        this.nickname = nickname;
        this.colorCode = colorCode;
    }

    public async Task ConnectAsync()
    {
        serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        client = new UdpClient(0);
        client.Connect(serverEndPoint);
        await SendNicknameAsync();

        AppDomain.CurrentDomain.ProcessExit += async (s, e) => await SendExitMessageAsync();

        _ = Task.Run(ReceiveMessagesAsync);
        _ = Task.Run(SendMessagesAsync);
        await Task.Delay(-1);
    }

    private async Task SendNicknameAsync()
    {
        var nickMessage = $"NICK|{nickname}|{colorCode}";
        var data = Encoding.UTF8.GetBytes(nickMessage);
        await client.SendAsync(data, data.Length);
    }

    private async Task SendExitMessageAsync()
    {
        var message = "EXIT";
        var data = Encoding.UTF8.GetBytes(message);
        await client.SendAsync(data, data.Length);
    }

    private async Task ReceiveMessagesAsync()
    {
        while (true)
        {
            var result = await client.ReceiveAsync();
            var message = Encoding.UTF8.GetString(result.Buffer);

            if (message.StartsWith("HISTORY|"))
            {
                var history = message.Substring(8);
                var lines = history.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        DisplayMessage(line);
                }
                continue;
            }

            DisplayMessage(message);
        }
    }

    private void DisplayMessage(string message)
    {
        var parts = message.Split('|');
        if (parts.Length >= 4)
        {
            var time = parts[0];
            var sender = parts[1];
            var content = parts[2];
            var color = int.Parse(parts[3]);

            SetConsoleColor(color);
            Console.WriteLine($"[{time}] {sender}: {content}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    private void SetConsoleColor(int colorCode)
    {
        switch (colorCode)
        {
            case 1: Console.ForegroundColor = ConsoleColor.Blue; break;
            case 2: Console.ForegroundColor = ConsoleColor.Green; break;
            case 3: Console.ForegroundColor = ConsoleColor.Cyan; break;
            case 4: Console.ForegroundColor = ConsoleColor.Red; break;
            case 5: Console.ForegroundColor = ConsoleColor.Magenta; break;
            case 6: Console.ForegroundColor = ConsoleColor.Yellow; break;
            case 7: Console.ForegroundColor = ConsoleColor.White; break;
            case 8: Console.ForegroundColor = ConsoleColor.Gray; break;
            case 9: Console.ForegroundColor = ConsoleColor.DarkBlue; break;
            case 10: Console.ForegroundColor = ConsoleColor.DarkGreen; break;
            case 11: Console.ForegroundColor = ConsoleColor.DarkCyan; break;
            case 12: Console.ForegroundColor = ConsoleColor.DarkRed; break;
            case 13: Console.ForegroundColor = ConsoleColor.DarkMagenta; break;
            case 14: Console.ForegroundColor = ConsoleColor.DarkYellow; break;
            case 15: Console.ForegroundColor = ConsoleColor.DarkGray; break;
            default: Console.ForegroundColor = ConsoleColor.Gray; break;
        }
    }

    private async Task SendMessagesAsync()
    {
        while (true)
        {
            Console.Write("Відправте повідомлення на сервер: ");
            var message = Console.ReadLine();

            if (message == "exit")
            {
                await SendExitMessageAsync();
                break;
            }

            var data = Encoding.UTF8.GetBytes($"MSG|{message}");
            await client.SendAsync(data, data.Length);
        }

        client.Close();
        Console.WriteLine("Відключено від сервера.");
    }
}

public class ConsoleMessageHandler : IMessageHandler
{
    public void HandleMessage(string message)
    {
        Console.WriteLine(message);
    }
}

class ClientProgram
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "CLIENT SIDE";

        string serverIp = "127.0.0.1";
        if (args.Length > 0)
            serverIp = args[0];

        Console.Write("Enter nickname: ");
        string nickname = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(nickname)) nickname = "User";

        Console.Write("Enter color (1-15): ");
        int colorCode = 1;
        int.TryParse(Console.ReadLine(), out colorCode);
        if (colorCode < 1 || colorCode > 15) colorCode = 1;

        var handler = new ConsoleMessageHandler();
        var client = new UdpChatClient(serverIp, 9000, handler, nickname, colorCode);
        await client.ConnectAsync();
    }
}
