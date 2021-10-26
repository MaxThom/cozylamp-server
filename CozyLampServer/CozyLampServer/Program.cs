using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

//app.UseHttpsRedirection();

int clean_timeout_sec = 20;
int device_timeout_sec = 30;

var groups = new Dictionary<string, List<Device>>();

/*
 * Each device has a group key and a device key (that can be modified on the esp32 web ui).
 * On ON, the lamp send a request saying its on, the api returns if another device in the group is ON.
 * If so, the device change to the ThinkOfYou light. Repeat every 10 sec
 * On OFF, the lamp stop the 10 sec request.
 * The API must detect the timeout if one lamp doesnt send its refresh and remove it from the group.
 * The dictonary holding the connection key must be deleted if all devices are off and will be recreated on ON request.
 */

app.MapGet("/api/health", () =>
{
    return "All system operational Commander !";
})
.WithName("Health");

app.MapGet("/api/updatestatus/{device_key}/{group_key}", (string device_key, string group_key) =>
{
    if (!groups.ContainsKey(group_key))
        groups[group_key] = new List<Device>();
    
    if (groups[group_key].Count(x => x.Name.Equals(device_key)) == 0)
        groups[group_key].Add(new Device()
        {
            Name = device_key,
            LastUpdate = DateTime.Now
        });
    else
        groups[group_key].Where(x => x.Name.Equals(device_key)).First().LastUpdate = DateTime.UtcNow;

    return groups[group_key].Where(x => !x.Name.Equals(device_key));
})
.WithName("UpdateStatus");

async Task removeOldConnection()
{
    while (true)
    {
        foreach (var group in groups.ToList())
        {
            foreach (var device in group.Value.ToList())
            {
                if ((DateTime.UtcNow - device.LastUpdate).TotalSeconds >= device_timeout_sec)
                {
                    group.Value.RemoveAll(x => x.Name == device.Name);
                }
            }
            if (group.Value.Count == 0)
            {
                groups.Remove(group.Key);
            }
        }

        await Task.Delay(clean_timeout_sec * 1000);
    }
}

app.MapGet("/api/groups", () =>
{
    return groups.ToArray();
})
.WithName("GetGroups");

app.MapGet("/api/settings", () =>
{
    return new Settings
    {
        clean_timeout_sec = clean_timeout_sec,
        device_timeout_sec = clean_timeout_sec
    };
})
.WithName("GetSettings");

app.MapPost("/api/settings", (Settings settings) =>
{
    clean_timeout_sec = settings.clean_timeout_sec;
    device_timeout_sec = settings.clean_timeout_sec;
    return settings;
})
.WithName("SetSettings");

_ = removeOldConnection().ConfigureAwait(false);
app.Run();

internal class Device
{
    public string Name { get; set; } = "";
    public DateTime LastUpdate { get; set; }
}

internal class Settings
{
    public int clean_timeout_sec { get; set; }
    public int device_timeout_sec { get; set; }
}