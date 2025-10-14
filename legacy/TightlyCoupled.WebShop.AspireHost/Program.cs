using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

// Papercut container
var papercut = builder.AddContainer("papercut", "jijiechen/papercut", "latest")
  .WithEndpoint("smtp", e =>
  {
      e.TargetPort = 25;
      e.Port = 25;
      e.Protocol = ProtocolType.Tcp;
      e.UriScheme = "smtp";
  })
  .WithEndpoint("ui", e =>
  {
      e.TargetPort = 37408;
      e.Port = 37408;
      e.UriScheme = "http";
  });

var sqlPassword = builder.AddParameter("sql-password", "YourStrong!Passw0rd", secret: true);

// Add SQL Server container with a persistent volume
var sql = builder.AddSqlServer("sql", password: sqlPassword)
                 .WithDataVolume()
                 .WithLifetime(ContainerLifetime.Persistent);

// Add a database inside that SQL Server
var db = sql.AddDatabase("TightlyCoupledWebShop");

// Your web project
var web = builder.AddProject<Projects.TightlyCoupled_WebShop>("web")
    .WithHttpHealthCheck("/health")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();
