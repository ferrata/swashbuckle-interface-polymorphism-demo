using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using Adyen.Model.Checkout.Action;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace demo
{
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers(options => options.EnableEndpointRouting = false);
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Sample Web API",
                    Version = "v1",
                    Description = "Sample Web API to demonstrate a workaround for Swashbuckle to reflect polymorphic hierarchy implemented with C# interfaces",
                    Contact = new OpenApiContact
                    {
                        Name = "Sasha Goloshchapov"
                    }
                });

                c.EnableAnnotations(enableAnnotationsForInheritance: true, enableAnnotationsForPolymorphism: true);
                c.UseAllOfToExtendReferenceSchemas();
                c.UseAllOfForInheritance();
                c.UseOneOfForPolymorphism();

                c.SelectDiscriminatorNameUsing(type =>
                {
                    return type.Name switch
                    {
                        nameof(PaymentResponseAction) => "type",
                        _ => null
                    };
                });

                c.SelectDiscriminatorValueUsing(subType =>
                {
                    return subType.Name switch
                    {
                        nameof(CheckoutAwaitAction) => "await",
                        nameof(CheckoutBankTransferAction) => "bank",
                        nameof(CheckoutDonationAction) => "donation",
                        nameof(CheckoutOneTimePasscodeAction) => "oneTimePasscode",
                        _ => null
                    };
                });

                var actionTypes = new[]
                {
                    GenerateReparentedType(typeof(CheckoutAwaitAction), typeof(PaymentResponseAction)),
                    GenerateReparentedType(typeof(CheckoutBankTransferAction), typeof(PaymentResponseAction)),
                    GenerateReparentedType(typeof(CheckoutDonationAction), typeof(PaymentResponseAction)),
                    GenerateReparentedType(typeof(CheckoutOneTimePasscodeAction), typeof(PaymentResponseAction)),
                };

                c.SelectSubTypesUsing(type =>
                {
                    var allTypes = typeof(Startup).Assembly.GetTypes().ToArray();
                    return type.Name switch
                    {
                        nameof(PaymentResponseAction) => new[] { typeof(PaymentResponseAction) }.Union(actionTypes),
                        nameof(IPaymentResponseAction) => new[] { typeof(PaymentResponseAction) }.Union(actionTypes),
                        _ => allTypes.Where(t => t.IsSubclassOf(type))
                    };
                });
            });
        }

        [DataContract]
        [SwaggerDiscriminator("type")]
        public class PaymentResponseAction : IPaymentResponseAction
        {
            [JsonProperty(PropertyName = "type")]
            public string Type { get; set; }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseSwagger();
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Web API V1"); });
            app.UseMvc(route =>
            {
                route.MapRoute(
                    name: "default",
                    template: "{Controller=Name}/{action=Name}/{id?}"
                );
            });
        }

        private static Type GenerateReparentedType(Type originalType, Type parent)
        {
            var assemblyBuilder =
                AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("hack"), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("hack");
            var typeBuilder = moduleBuilder.DefineType(originalType.Name, TypeAttributes.Public, parent);

            foreach (var property in originalType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var newProperty = typeBuilder
                    .DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

                var getMethod = property.GetMethod;
                if (getMethod is not null)
                {
                    var getMethodBuilder = typeBuilder
                        .DefineMethod(getMethod.Name, getMethod.Attributes, getMethod.ReturnType, Type.EmptyTypes);
                    getMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
                    newProperty.SetGetMethod(getMethodBuilder);
                }

                var setMethod = property.SetMethod;
                if (setMethod is not null)
                {
                    var setMethodBuilder = typeBuilder
                        .DefineMethod(setMethod.Name, setMethod.Attributes, setMethod.ReturnType, Type.EmptyTypes);
                    setMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
                    newProperty.SetSetMethod(setMethodBuilder);
                }
            }

            var type = typeBuilder.CreateType();
            return type ?? throw new InvalidOperationException($"Unable to generate a re-parented type for {originalType}.");
        }
    }
}
