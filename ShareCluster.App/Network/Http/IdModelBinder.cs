﻿using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Network.Http
{
    /// <summary>
    /// Model binding for MVC of type <see cref="Id"/>.
    /// </summary>
    public class IdModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext.ModelType != typeof(Id)) throw new NotSupportedException();

            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName).FirstValue;
            if(!Id.TryParse(value, out Id hash))
            {
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Can't parse Id.");
                return Task.CompletedTask;
            }

            // success
            bindingContext.Result = ModelBindingResult.Success(hash);
            return Task.CompletedTask;
        }
    }
}