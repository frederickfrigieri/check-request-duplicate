using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IO;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using WebApplicationTesteMiddleware.Controllers;

namespace WebApplicationTesteMiddleware
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IMemoryCache memoryCache)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();


            app.Use(async (context, next) =>
            {
                var syncIoFeature = context.Features.Get<IHttpBodyControlFeature>();

                if (syncIoFeature != null) syncIoFeature.AllowSynchronousIO = true;


                Console.WriteLine("Passou aqui antes de chegar na requisição");


                if (context.Request.Path == "/weatherForecast" && context.Request.Method == "POST")
                {
                    var requestBody = await ObterRequestBody(context);

                    var pedido = System.Text.Json.JsonSerializer.Deserialize<PedidoRequest>(requestBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    memoryCache.TryGetValue(pedido.NumeroPedido.ToString(), out object valorCache);

                    if (valorCache == null) memoryCache.Set(pedido.NumeroPedido.ToString(), pedido);
                    else throw new Exception("Já existe uma requisição em andamento");

                    await next.Invoke();

                    memoryCache.Remove(pedido.NumeroPedido.ToString());

                    Console.WriteLine("Passou aqui depois do return da requisição");
                }
                else
                    await next.Invoke();
            });


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static async Task<string> ObterRequestBody(HttpContext context)
        {
            RecyclableMemoryStreamManager recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();

            context.Request.EnableBuffering();

            using var requestStream = recyclableMemoryStreamManager.GetStream();
            await context.Request.Body.CopyToAsync(requestStream);

            var requestBody = ReadStreamInChunks(requestStream);

            context.Request.Body.Position = 0;

            return requestBody;
        }

        private static string ReadStreamInChunks(Stream stream)
        {
            const int readChunkBufferLength = 4096;

            stream.Seek(0, SeekOrigin.Begin);

            using var textWriter = new StringWriter();
            using var reader = new StreamReader(stream);
            var readChunk = new char[readChunkBufferLength];
            int readChunkLength;
            do
            {
                readChunkLength = reader.ReadBlock(readChunk, 0, readChunkBufferLength);
                textWriter.Write(readChunk, 0, readChunkLength);
            } while (readChunkLength > 0);

            return textWriter.ToString();
        }

    }
}
