using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace TorrentClient.Protocol
{
    /// <summary>
    /// Сервис для выполнения HTTP/HTTPS запросов через HttpClient (синглтон)
    /// </summary>
    internal sealed class HttpClientService : IDisposable
    {
        private static readonly Lazy<HttpClientService> _instance = new(() => new HttpClientService());
        private readonly HttpClient _httpClient;
        private readonly object _lockObject = new();
        private bool _disposed = false;

        /// <summary>
        /// Единственный экземпляр сервиса (синглтон)
        /// </summary>
        public static HttpClientService Instance => _instance.Value;

        /// <summary>
        /// Приватный конструктор для синглтона
        /// </summary>
        private HttpClientService()
        {
            // Используем SocketsHttpHandler для настройки параметров сокета
            var handler = new SocketsHttpHandler
            {
                // Разрешаем все SSL сертификаты (для трекеров)
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (message, cert, chain, errors) => true
                },
                // Используем автоматическое сжатие
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                // Разрешаем перенаправления
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                // Настройка параметров сокета
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                    {
                        // Отключаем алгоритм Nagle (NoDelay = true)
                        NoDelay = true,
                        // LingerState = 0 (немедленное закрытие соединения)
                        LingerState = new LingerOption(false, 0)
                    };

                    try
                    {
                        await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30),
                DefaultRequestHeaders =
                {
                    { "User-Agent", "TorrentClient/1.0" },
                    { "Connection", "close" }
                }
            };
        }

        /// <summary>
        /// Выполняет HTTP GET запрос
        /// </summary>
        public async Task<HttpResponse> GetAsync(
            string url, 
            Dictionary<string, string>? headers = null, 
            int timeoutSeconds = 30, 
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpClientService));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"Invalid URL: {url}", nameof(url));
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                
                // Добавляем пользовательские заголовки
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        // Проверяем, не является ли заголовок стандартным
                        if (string.Equals(header.Key, "User-Agent", StringComparison.OrdinalIgnoreCase))
                        {
                            request.Headers.UserAgent.Clear();
                            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(header.Value));
                        }
                        else if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            request.Content ??= new ByteArrayContent(Array.Empty<byte>());
                            request.Content.Headers.ContentType = new MediaTypeHeaderValue(header.Value);
                        }
                        else
                        {
                            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }
                }

                // Устанавливаем таймаут для этого запроса
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token).ConfigureAwait(false);

                // Читаем тело ответа
                var bodyBytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);

                // Преобразуем заголовки ответа
                var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in response.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }
                foreach (var header in response.Content.Headers)
                {
                    responseHeaders[header.Key] = string.Join(", ", header.Value);
                }

                return new HttpResponse
                {
                    StatusCode = (int)response.StatusCode,
                    StatusMessage = response.ReasonPhrase ?? "Unknown",
                    Headers = responseHeaders,
                    Body = bodyBytes
                };
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Request was cancelled", cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw new TimeoutException($"Request to {url} timed out after {timeoutSeconds} seconds");
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"HTTP request failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_disposed)
                    return;

                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Представляет HTTP ответ
    /// </summary>
    internal class HttpResponse
    {
        public int StatusCode { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[] Body { get; set; } = Array.Empty<byte>();

        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode < 300;

        public string GetBodyAsString() => Encoding.UTF8.GetString(Body);
    }
}

