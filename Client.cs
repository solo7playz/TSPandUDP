using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class CurrencyClient
{
    const int PORT = 5252;
    static UdpClient udpClient = new UdpClient();

    static async Task Main()
    {
        Console.WriteLine("Клиент запущен. Введите валюты в формате: USD EURO");
        var serverEndPoint = new IPEndPoint(IPAddress.Loopback, PORT);

        while (true)
        {
            Console.Write("Введите валюты (или 'exit' для выхода): ");
            string input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            byte[] data = Encoding.UTF8.GetBytes(input);
            await udpClient.SendAsync(data, data.Length, serverEndPoint);

            var result = await udpClient.ReceiveAsync();
            string response = Encoding.UTF8.GetString(result.Buffer);
            Console.WriteLine($"Ответ сервера: {response}");
        }
    }
}