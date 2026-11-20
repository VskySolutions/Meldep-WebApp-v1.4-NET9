using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Vsky.Api.Extensions
{
    /// <summary>
    /// The API documentation: the OpenAPI document at /openapi/v1.json and the Scalar page at /scalar.
    /// </summary>
    public static class ApiDocsExtensions
    {
        private const string DocumentName = "v1";

        public static IServiceCollection AddApiDocs(this IServiceCollection services)
        {
            // The document covers about 890 endpoints, so it is built once and then served from memory.
            services.AddOutputCache();
            services.TryAddEnumerable(ServiceDescriptor.Transient<IApiDescriptionProvider, FormModelDescriptionProvider>());

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc(DocumentName, new OpenApiInfo
                {
                    Title = "Meldep 3.2 APIs",
                    Version = "v1"
                });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "The JWT from POST /auth/login. Enter the token only, without the word Bearer."
                });
                // The web app sends the active site on every request (boot/axios.js), and the API reads it only
                // from these headers (GlobalVariableMiddleware). Declaring them here makes the docs page send
                // them too, so a call from the docs is checked exactly like the same call from the app.
                // GET /authorization/my-sites lists the site IDs a token can use.
                options.AddSecurityDefinition("SiteId", new OpenApiSecurityScheme
                {
                    Name = "X-Site-Id",
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "Optional. The active site ID (a GUID, not the token). Leave empty to use the site the app opens at login (your last-used site). To work in another of your sites, enter its ID: GET /authorization/my-sites lists them."
                });
                options.AddSecurityDefinition("SiteTimezone", new OpenApiSecurityScheme
                {
                    Name = "X-Site-Timezone",
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Description = "Optional. The active site's time zone, as the web app sends it (for example \"India Standard Time\"), so dates match the app."
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>(),
                    [new OpenApiSecuritySchemeReference("SiteId", document)] = new List<string>(),
                    [new OpenApiSecuritySchemeReference("SiteTimezone", document)] = new List<string>()
                });
            });

            return services;
        }

        public static WebApplication MapApiDocs(this WebApplication app)
        {
            app.MapSwagger("/openapi/{documentName}.json").CacheOutput(policy => policy.Expire(TimeSpan.FromDays(1)));
            app.MapScalarApiReference(options =>
            {
                options.Title = "Meldep 3.2 APIs";
                // Hides Scalar's own toolbar (Share, Generate SDK, Deploy), which can send the document to Scalar.
                options.ShowDeveloperTools = DeveloperToolsVisibility.Never;
            });
            return app;
        }
    }

    /// <summary>
    /// Describes a [FromForm] model by its own fields only. By default every field of every model it links
    /// to becomes a separate form field as well: 171,000 fields for one upload action, a 430 MB document.
    /// </summary>
    internal sealed class FormModelDescriptionProvider : IApiDescriptionProvider
    {
        // After the built-in provider (-1000), which creates the descriptions this one trims.
        public int Order => 0;

        public void OnProvidersExecuting(ApiDescriptionProviderContext context)
        {
        }

        public void OnProvidersExecuted(ApiDescriptionProviderContext context)
        {
            foreach (var description in context.Results)
            {
                // A dotted name, such as Project.Coordinator.Person.FirstName, is a field of a linked model.
                var linkedFields = description.ParameterDescriptions
                    .Where(parameter => (parameter.Source == BindingSource.Form || parameter.Source == BindingSource.FormFile)
                        && parameter.Name.Contains('.'))
                    .ToList();

                foreach (var field in linkedFields)
                    description.ParameterDescriptions.Remove(field);
            }
        }
    }
}
