using Climate.ArduinoBridge;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<BridgeOptions>().Bind(builder.Configuration)
    .Validate(options => options.IsValid(), "Configure Arduino, Backend, Device and Buffer; production requires HTTPS.")
    .ValidateOnStart();
builder.Services.AddHttpClient("telemetry");
builder.Services.AddSingleton<PersistentQueue>();
builder.Services.AddSingleton<ISerialDataSource, SerialDataSource>();
builder.Services.AddHostedService<AcquisitionWorker>();
builder.Services.AddHostedService<TransmissionWorker>();
await builder.Build().RunAsync();
