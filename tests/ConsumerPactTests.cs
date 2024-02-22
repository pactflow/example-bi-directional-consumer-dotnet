﻿using PactNet;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Consumer;
using System.Net.Http;
using PactNet.Matchers;
using System;

namespace tests
{
    public class ConsumerPactTests
    {
        private readonly IPactBuilderV3 pact;
        private readonly int port = 9000;


        public ConsumerPactTests(ITestOutputHelper output)
        {
            var config = new PactConfig
            {
                PactDir = "../../../pacts/",
                LogLevel = PactLogLevel.Debug,
                Outputters = (new[]
           {
                // NOTE: We default to using a ConsoleOutput, however xUnit 2 does not capture the console output,
                // so a custom outputter is required.
                new XUnitOutput(output)
            }),
                DefaultJsonSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                }
            };

            String provider = Environment.GetEnvironmentVariable("PACT_PROVIDER");
            // you select which specification version you wish to use by calling either V2 or V3
            IPactV3 pact = Pact.V3("pactflow-example-bi-directional-consumer-dotnet", provider != null ? provider : "pactflow-example-bi-directional-provider-dotnet" , config);

            // the pact builder is created in the constructor so it's unique to each test
            this.pact = pact.UsingNativeBackend(port);
        }

        [Fact]
        public async void GetProducts_WhenCalled_ReturnsAllProducts()
        {
            //Arrange
            pact
                .UponReceiving("a request to retrieve all products")
                .WithRequest(HttpMethod.Get, "/Products")
                .WillRespond()
                .WithStatus(System.Net.HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json;")
                .WithJsonBody(Match.MinType(new
                {
                    id = 27,
                    type = "food"
                }, 1));

            //Act
            await pact.VerifyAsync(async ctx =>
            {
                var client = new ProductClient();
               var products = await client.GetProducts(ctx.MockServerUri.AbsoluteUri, null);

                //Assert
                Assert.IsType<int>(products[0].id);
                Assert.IsType<string>(products[0].type);
            });
            //the mock server is no longer running once VerifyAsync returns
        }

        [Fact]
        public async void GetProduct_WhenCalledWithExistingId_ReturnsProduct()
        {
            pact
                .UponReceiving("a request to retrieve a product with existing id")
                .WithRequest(HttpMethod.Get, "/Products/27")
                .WillRespond()
                .WithStatus(System.Net.HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json;")
                .WithJsonBody(new
                {
                    id = Match.Type(27),
                    type = Match.Type("food")
                });

            //Act
            await pact.VerifyAsync(async ctx =>
            {
                var client = new ProductClient();
                var product = await client.GetProduct(ctx.MockServerUri.AbsoluteUri, 27, null);

                //Assert
                Assert.IsType<int>(product.id);
                Assert.IsType<string>(product.type);
            });
        }

        [Fact]
        public async void GetProduct_WhenCalledWithInvalidID_ReturnsError()
        {
            pact
                .UponReceiving("a request to retrieve a product id that does not exist")
                .WithRequest(HttpMethod.Get, "/Products/10")
                .WillRespond()
                .WithStatus(System.Net.HttpStatusCode.NotFound)
                .WithHeader("Content-Type", "application/json;");
     

            //Act
            await pact.VerifyAsync(async ctx =>
            {
                var client = new ProductClient();

                //Assert
                var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetProduct(ctx.MockServerUri.AbsoluteUri, 10, null));
                Assert.Equal("Response status code does not indicate success: 404 (Not Found).", ex.Message);
            });
        }
    }
}
