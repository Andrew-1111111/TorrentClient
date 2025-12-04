using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Вспомогательный класс для выполнения HTTP/HTTPS запросов через TcpClient и SslStream
    /// </summary>
    internal static class HttpTcpClient
    {
        private const int DefaultTimeoutSeconds = 30;
        private const string UserAgent = "TorrentClient/1.0";

        /// <summary>
        /// Выполняет HTTP GET запрос
        /// </summary>
        public static async Task<HttpResponse> GetAsync(string url, Dictionary<string, string>? headers = null, int timeoutSeconds = DefaultTimeoutSeconds, CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Invalid URL: {url}", nameof(url));
            }

            return await SendRequestAsync(uri, "GET", null, headers, timeoutSeconds, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Выполняет HTTP запрос
        /// </summary>
        private static async Task<HttpResponse> SendRequestAsync(
            Uri uri,
            string method,
            byte[]? body,
            Dictionary<string, string>? headers,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            TcpClient? tcpClient = null;
            Stream? stream = null;
            SslStream? sslStream = null;

            try
            {
                // Разрешаем DNS для получения всех адресов (IPv4 и IPv6)
                var addresses = await Dns.GetHostAddressesAsync(uri.Host, cancellationToken).ConfigureAwait(false);
                
                // Сортируем: IPv6 первым, затем IPv4
                var sortedAddresses = addresses
                    .OrderByDescending(a => a.AddressFamily == AddressFamily.InterNetworkV6)
                    .ToArray();

                Exception? lastException = null;
                var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);

                // Пробуем подключиться к каждому адресу
                foreach (var address in sortedAddresses)
                {
                    try
                    {
                        tcpClient = new TcpClient(address.AddressFamily)
                        {
                            NoDelay = true,
                            LingerState = new LingerOption(false, 0),  // LingerState = 0
                            ReceiveTimeout = timeoutSeconds * 1000,
                            SendTimeout = timeoutSeconds * 1000
                        };

                        // Включаем DualMode для IPv6 сокетов
                        if (address.AddressFamily == AddressFamily.InterNetworkV6)
                        {
                            tcpClient.Client.DualMode = true;
                        }

                        var endpoint = new IPEndPoint(address, port);
                        await TaskTimeoutHelper.TimeoutAsync(
                            tcpClient.ConnectAsync(endpoint.Address, endpoint.Port, cancellationToken),
                            TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);

                        stream = tcpClient.GetStream();

                        // Для HTTPS используем SslStream
                        if (uri.Scheme == "https")
                        {
                            sslStream = new SslStream(stream, false, ValidateServerCertificate, null);
                            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                            {
                                TargetHost = uri.Host
                            }, cancellationToken).ConfigureAwait(false);
                            stream = sslStream;
                        }

                        break; // Успешно подключились
                    }
                    catch (Exception ex)
                    {
                        tcpClient?.Dispose();
                        tcpClient = null;
                        stream?.Dispose();
                        stream = null;
                        sslStream?.Dispose();
                        sslStream = null;
                        lastException = ex;
                        // Пробуем следующий адрес
                    }
                }

                if (tcpClient == null || stream == null)
                {
                    throw lastException ?? new SocketException((int)SocketError.HostNotFound);
                }

                // Формируем HTTP запрос
                var requestBuilder = new StringBuilder();
                var path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
                requestBuilder.AppendLine($"{method} {path} HTTP/1.1");
                requestBuilder.AppendLine($"Host: {uri.Host}");
                requestBuilder.AppendLine($"User-Agent: {UserAgent}");
                requestBuilder.AppendLine("Connection: close");

                // Добавляем пользовательские заголовки
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        requestBuilder.AppendLine($"{header.Key}: {header.Value}");
                    }
                }

                // Добавляем тело запроса, если есть
                if (body != null && body.Length > 0)
                {
                    requestBuilder.AppendLine($"Content-Length: {body.Length}");
                    requestBuilder.AppendLine("Content-Type: application/x-www-form-urlencoded");
                }

                requestBuilder.AppendLine(); // Пустая строка после заголовков

                var requestBytes = Encoding.ASCII.GetBytes(requestBuilder.ToString());

                // Отправляем запрос
                await TaskTimeoutHelper.TimeoutAsync(
                    stream.WriteAsync(requestBytes.AsMemory(), cancellationToken),
                    TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);
                
                if (body != null && body.Length > 0)
                {
                    await TaskTimeoutHelper.TimeoutAsync(
                        stream.WriteAsync(body.AsMemory(), cancellationToken),
                        TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);
                }

                await TaskTimeoutHelper.TimeoutAsync(
                    stream.FlushAsync(cancellationToken),
                    TimeSpan.FromSeconds(5)).ConfigureAwait(false);

                // Читаем ответ
                var response = await ReadResponseAsync(stream, timeoutSeconds, cancellationToken).ConfigureAwait(false);

                return response;
            }
            finally
            {
                // Освобождаем ресурсы
                sslStream?.Dispose();
                stream?.Dispose();
                tcpClient?.Dispose();
            }
        }

        /// <summary>
        /// Читает HTTP ответ из потока
        /// </summary>
        private static async Task<HttpResponse> ReadResponseAsync(Stream stream, int timeoutSeconds, CancellationToken cancellationToken)
        {
            return await TaskTimeoutHelper.TimeoutAsync(
                ReadResponseInternalAsync(stream, cancellationToken),
                TimeSpan.FromSeconds(timeoutSeconds)).ConfigureAwait(false);
        }

        private static async Task<HttpResponse> ReadResponseInternalAsync(Stream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var responseBuilder = new StringBuilder();
            var headersRead = false;
            var contentLength = -1;
            var statusCode = 200;
            var statusMessage = "OK";
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Читаем заголовки
            while (!headersRead)
            {
                var bytesRead = await TaskTimeoutHelper.TimeoutAsync(
                    stream.ReadAsync(buffer.AsMemory(), cancellationToken),
                    TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                responseBuilder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                var responseText = responseBuilder.ToString();

                // Ищем конец заголовков (две пустые строки)
                var headerEndIndex = responseText.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEndIndex >= 0)
                {
                    var headerText = responseText.Substring(0, headerEndIndex);
                    var headerLines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);

                    // Парсим статусную строку
                    if (headerLines.Length > 0)
                    {
                        var statusParts = headerLines[0].Split(new[] { ' ' }, 3);
                        if (statusParts.Length >= 2)
                        {
                            int.TryParse(statusParts[1], out statusCode);
                            if (statusParts.Length >= 3)
                            {
                                statusMessage = statusParts[2];
                            }
                        }
                    }

                    // Парсим заголовки
                    for (int i = 1; i < headerLines.Length; i++)
                    {
                        var colonIndex = headerLines[i].IndexOf(':');
                        if (colonIndex > 0)
                        {
                            var key = headerLines[i].Substring(0, colonIndex).Trim();
                            var value = headerLines[i].Substring(colonIndex + 1).Trim();
                            headers[key] = value;

                            if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(value, out contentLength);
                            }
                        }
                    }

                    // Удаляем заголовки из буфера
                    var bodyStartIndex = headerEndIndex + 4;
                    responseBuilder.Clear();
                    if (bodyStartIndex < responseText.Length)
                    {
                        responseBuilder.Append(responseText.Substring(bodyStartIndex));
                    }

                    headersRead = true;
                }
            }

            // Читаем тело ответа
            List<byte> bodyBytes = [];
            if (responseBuilder.Length > 0)
            {
                bodyBytes.AddRange(Encoding.UTF8.GetBytes(responseBuilder.ToString()));
            }

            if (contentLength > 0)
            {
                while (bodyBytes.Count < contentLength)
                {
                    var bytesToRead = Math.Min(buffer.Length, contentLength - bodyBytes.Count);
                    var bytesRead = await TaskTimeoutHelper.TimeoutAsync(
                        stream.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken),
                        TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    bodyBytes.AddRange(buffer.Take(bytesRead));
                }
            }
            else
            {
                // Читаем до конца потока, если Content-Length не указан
                while (true)
                {
                    var bytesRead = await TaskTimeoutHelper.TimeoutAsync(
                        stream.ReadAsync(buffer.AsMemory(), cancellationToken),
                        TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    bodyBytes.AddRange(buffer.Take(bytesRead));
                }
            }

            return new HttpResponse
            {
                StatusCode = statusCode,
                StatusMessage = statusMessage,
                Headers = headers,
                Body = bodyBytes.ToArray()
            };
        }

        /// <summary>
        /// Валидация SSL сертификата (принимаем все сертификаты)
        /// </summary>
        private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // Для трекеров принимаем все сертификаты
            return true;
        }
    }

    // HttpResponse класс перемещен в HttpClientService.cs
}

