using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Model binding for MVC of type <see cref="Hash"/>.
    /// </summary>
    public class HashModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext.ModelType != typeof(Hash)) throw new NotSupportedException();

            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
            if(!Hash.TryParse(value, out Hash hash))
            {
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Can't parse hash.");
                return Task.CompletedTask;
            }

            // success
            bindingContext.Result = ModelBindingResult.Success(hash);
            return Task.CompletedTask;
        }
    }
}
