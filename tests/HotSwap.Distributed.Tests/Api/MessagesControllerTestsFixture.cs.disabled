using HotSwap.Distributed.Api;
using HotSwap.Distributed.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace HotSwap.Distributed.Tests.Api;

public class MessagesControllerTestsFixture : IDisposable
{
    public WebApplicationFactory<Program> Factory { get; }
    public Mock<IMessageQueue> MockQueue { get; }
    public Mock<IMessagePersistence> MockPersistence { get; }

    public MessagesControllerTestsFixture()
    {
        MockQueue = new Mock<IMessageQueue>();
        MockPersistence = new Mock<IMessagePersistence>();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
        {
            // Set environment to Test to disable background services that cause test hangs
            builder.UseEnvironment("Test");

            builder.ConfigureServices(services =>
            {
                // Replace real services with mocks
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(IMessageQueue) ||
                    d.ServiceType == typeof(IMessagePersistence)).ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton(MockQueue.Object);
                services.AddSingleton(MockPersistence.Object);

                // Add test authentication
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "TestScheme";
                    options.DefaultChallengeScheme = "TestScheme";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", options => { });
            });
        });
    }

    public void Dispose()
    {
        Factory?.Dispose();
    }
}
