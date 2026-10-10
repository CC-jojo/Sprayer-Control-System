using System.IO;
using System.Text.Json;

namespace SprayerControl.App.Services;

/// <summary>记忆上次成功连接的串口名，下次启动优先匹配该端口，匹配不到再全量扫描。</summary>
public sealed class PortMemoryStore
{
    private readonly string _path;

    public PortMemoryStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SprayerTouchHMI",
            "last-port.json");
    }

    public string? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            using FileStream stream = File.OpenRead(_path);
            using JsonDocument document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("PortName", out JsonElement element)
                && element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : null;
        }
        catch
        {
            // 记忆损坏不影响正常扫描。
            return null;
        }
    }

    public void Save(string portName)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(new { PortName = portName }));
        }
        catch
        {
            // 记忆失败不影响连接。
        }
    }
}
