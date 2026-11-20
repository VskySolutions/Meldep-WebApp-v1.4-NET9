using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Vsky.Api.Extensions
{
    /// <summary>
    /// Keeps model parameters bound from the JSON body, as they were on .NET 7.
    /// Newer ASP.NET Core stops choosing the body for a model that has a [FromQuery] property, and
    /// BaseSearchModel has five, so every list filter (and each model that holds one) would arrive empty.
    /// </summary>
    public class BodyBindingConvention : IActionModelConvention
    {
        public void Apply(ActionModel action)
        {
            if (!action.Controller.Attributes.OfType<IApiBehaviorMetadata>().Any())
                return;

            foreach (var parameter in action.Parameters)
            {
                // The framework has already chosen a source for everything else: route, query, form, services.
                if (parameter.BindingInfo?.BindingSource != null)
                    continue;

                var type = parameter.ParameterInfo.ParameterType;
                if (!type.IsClass || type == typeof(string))
                    continue;

                parameter.BindingInfo ??= new BindingInfo();
                parameter.BindingInfo.BindingSource = BindingSource.Body;
            }
        }
    }
}
