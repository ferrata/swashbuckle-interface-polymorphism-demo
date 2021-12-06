using System.Collections.Generic;
using System.Net;
using Adyen.Model.Checkout;
using Adyen.Model.Checkout.Action;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace demo.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PaymentsController : ControllerBase
    {
        [HttpGet]
        [SwaggerResponse((int)HttpStatusCode.OK, "Payment Response", typeof(PaymentResponse))]
        public PaymentResponse GetPaymentAction()
        {
            var amount = new Amount("USD", 1);
            return new PaymentResponse
            {
                Amount = amount,
                FraudResult = new FraudResult(0, new List<FraudCheckResultContainer>()),
                Order = new CheckoutOrderResponse("", "", "", "", amount),
                Redirect = new Redirect(new Dictionary<string, string>(), Adyen.Model.Checkout.Redirect.MethodEnum.GET),
                Action = new CheckoutBankTransferAction { Type = "bank" }
            };
        }
    }
}