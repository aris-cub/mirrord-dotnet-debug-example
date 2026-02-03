using System.Text.Json;
using System.Web;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "redis";
var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";

ConnectionMultiplexer GetRedisConnection()
{
    return ConnectionMultiplexer.Connect($"{redisHost}:{redisPort}");
}

void AddEntry(string name, string message)
{
    using var redis = GetRedisConnection();
    var db = redis.GetDatabase();
    var entry = JsonSerializer.Serialize(new
    {
        name,
        message,
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    });
    db.ListLeftPush("entries", entry);
}

List<Dictionary<string, object>> GetEntries()
{
    using var redis = GetRedisConnection();
    var db = redis.GetDatabase();
    var entries = db.ListRange("entries", 0, -1);
    var result = new List<Dictionary<string, object>>();
    foreach (var entry in entries)
    {
        var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(entry!);
        if (parsed != null) result.Add(parsed);
    }
    return result;
}

string RenderPage(List<Dictionary<string, object>> entries)
{
    var entriesHtml = "";
    foreach (var entry in entries)
    {
        var name = HttpUtility.HtmlEncode(entry["name"].ToString());
        var message = HttpUtility.HtmlEncode(entry["message"].ToString());
        var timestamp = long.Parse(entry["timestamp"].ToString()!);
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString("yyyy-MM-dd HH:mm:ss");
        entriesHtml += $@"
        <div class=""entry"">
            <strong>{name}</strong>
            <p>{message}</p>
            <small>{date}</small>
        </div>";
    }

    return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>mirrord Guestbook</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        .header img {{
            max-width: 200px;
            margin-bottom: 20px;
        }}
        .entry-form {{
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            margin-bottom: 30px;
        }}
        .entry {{
            background: white;
            padding: 15px;
            margin-bottom: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        input[type=""text""], textarea {{
            width: 100%;
            padding: 8px;
            margin-bottom: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            box-sizing: border-box;
        }}
        button {{
            background-color: #4CAF50;
            color: white;
            padding: 10px 20px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
        }}
        button:hover {{
            background-color: #45a049;
        }}
        small {{
            color: #666;
        }}
    </style>
</head>
<body>
    <div class=""header"">
        <img src=""/images/mirrord.svg"" alt=""mirrord logo"">
        <h1>mirrord Guestbook</h1>
    </div>

    <div class=""entry-form"">
        <h2>Add New Entry</h2>
        <form method=""POST"">
            <input type=""text"" name=""name"" placeholder=""Your Name"" required>
            <textarea name=""message"" placeholder=""Your Message"" required></textarea>
            <button type=""submit"">Submit</button>
        </form>
    </div>

    <h2>Entries</h2>
    {entriesHtml}
</body>
</html>";
}

app.MapGet("/", () =>
{
    var entries = GetEntries();
    return Results.Content(RenderPage(entries), "text/html");
});

app.MapPost("/", async (HttpContext context) =>
{
    var form = await context.Request.ReadFormAsync();
    var name = form["name"].ToString();
    var message = form["message"].ToString();

    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(message))
    {
        AddEntry(name, message);
    }

    return Results.Redirect("/");
});

app.Run();
