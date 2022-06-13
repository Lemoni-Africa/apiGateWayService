using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using OnePaySystem.ApiGateway.Middleware;

namespace OnePaySystem.ApiGateway
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
            services.AddCors();
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ApiGateway", Version = "v1" });
            });
            /*services.AddDbContext<DataContext>(x => x.UseSqlServer(Configuration.GetConnectionString("ServiceStringMsSql")
                , builder =>
                {
                    builder.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                }));*/
            string authority = this.Configuration.GetSection("appSettings").GetSection("Authority").Value;
            //
            string validAudiencesString = this.Configuration.GetSection("appSettings").GetSection("ValidAudiences").Value;
            var validAudiencesArray = validAudiencesString.Split(';');
            List<string> validAudiences = new List<string>();
            foreach (var valid in validAudiencesArray)
            {
                validAudiences.Add(valid);
            }
            services.AddOcelot().AddCacheManager(x =>
            {
                x.WithDictionaryHandle();
            });
            services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer("Bearer", x =>
                {
                    x.RequireHttpsMetadata = false;
                    //x.SaveToken = true;
                    x.Authority = authority;
                    //x.Audience = "User";
                    x.TokenValidationParameters = new TokenValidationParameters
                    {

                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidAudiences = validAudiences


                    };
                });

            //services.AddScoped<IUnitOfWork, UnitOfWork>();
            //services.AddScoped<IPublisher,DirectExchangePublisher>();
           // services.AddScoped<IBilling, Billing>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseCors(
                options => options.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()
                );
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                
            }
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "ApiGateway v1"));

            app.UseHttpsRedirection();

            //app.UseValidateClientIp();
            //app.UseHttpsRedirection();
            // app.UseMiddleware<RequestResponseLoggingMiddleware>();
            
            app.UseMiddleware<ValidateClientIpMiddleware>();
            app.UseOcelot().Wait();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
