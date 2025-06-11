using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

class CurrencyServer
{
    const int PORT = 5252;
    static UdpClient udpServer = new UdpClient(PORT);
    static ConcurrentDictionary<string, ClientInfo> clients = new ConcurrentDictionary<string, ClientInfo>();
    static object lockObj = new object();

    static int maxRequestsPerClient = 5;
    static int blockDuration = 60 * 1000;

    static void Main()
    {
        Console.WriteLine($"Server started on port {PORT}");
        Task.Run(() => ListenAsync());

        while (true)
        {
            Console.WriteLine("Введите новое ограничение по запросам (или ENTER для оставить без изменений):");
            var input = Console.ReadLine();
            if (int.TryParse(input, out int newLimit))
            {
                maxRequestsPerClient = newLimit;
                Console.WriteLine($"Лимит запросов обновлен: {maxRequestsPerClient}");
            }
        }
    }

    static async Task ListenAsync()
    {
        while (true)
        {
            var result = await udpServer.ReceiveAsync();
            var remoteEndPoint = result.RemoteEndPoint;
            var message = Encoding.UTF8.GetString(result.Buffer);

            string clientKey = remoteEndPoint.ToString();

            if (!clients.ContainsKey(clientKey))
            {
                var clientInfo = new ClientInfo
                {
                    EndPoint = remoteEndPoint,
                    IP = remoteEndPoint.ToString(),
                    ConnectedAt = DateTime.Now,
                    RequestCount = 0,
                    IsBlockedUntil = DateTime.MinValue
                };
                clients[clientKey] = clientInfo;

                Console.WriteLine($"Клиент подключился: {clientKey} в {clientInfo.ConnectedAt}");
            }

            var client = clients[clientKey];

            if (DateTime.Now < client.IsBlockedUntil)
            {
                string blockMsg = "Вы достигли лимита запросов. Повторите попытку через минуту.";
                await SendAsync(blockMsg, remoteEndPoint);
                continue;
            }
            await ProcessRequestAsync(message, remoteEndPoint, client);
        }
    }

    static async Task ProcessRequestAsync(string message, IPEndPoint remoteEndPoint, ClientInfo client)
    {
        if (client.RequestCount >= maxRequestsPerClient)
        {
            client.IsBlockedUntil = DateTime.Now.AddMilliseconds(blockDuration);
            string msg = "Лимит запросов достигнут. Соединение закрыто на минуту.";
            await SendAsync(msg, remoteEndPoint);
            clients.TryRemove(remoteEndPoint.ToString(), out _);
            Console.WriteLine($"Клиент {remoteEndPoint} заблокирован на минуту.");
            return;
        }
        var parts = message.Trim().Split(' ');
        if (parts.Length != 2)
        {
            await SendAsync("Некорректный формат запроса. Используйте: <валюта1> <валюта2>", remoteEndPoint);
            return;
        }

        string currency1 = parts[0].ToUpper();
        string currency2 = parts[1].ToUpper();

        decimal rate = GetMockRate(currency1, currency2);

        string response = $"Курс {currency1} к {currency2}: {rate}";
        await SendAsync(response, remoteEndPoint);

        client.RequestCount++;
        Console.WriteLine($"Обработан запрос от {remoteEndPoint}. Запросов: {client.RequestCount}");
    }

    static decimal GetMockRate(string c1, string c2)
    {
        Random rnd = new Random(c1.GetHashCode() ^ c2.GetHashCode());
        return Math.Round((decimal)(rnd.NextDouble() * 2 + 0.5), 4);
    }

    static async Task SendAsync(string message, IPEndPoint endPoint)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        await udpServer.SendAsync(data, data.Length, endPoint);
    }

    class ClientInfo
    {
        public IPEndPoint EndPoint { get; set; }
        public string IP { get; set; }
        public DateTime ConnectedAt { get; set; }
        public int RequestCount { get; set; }
        public DateTime IsBlockedUntil { get; set; }
    }
}