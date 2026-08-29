using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class SplitDetailsPage : ContentPage
{
    private readonly ExpenseSplit _split;
    private readonly ISplitService _splitService;
    private readonly IPaymentRequestService _paymentRequestService;
    private readonly IPaymentGatewayService _paymentGatewayService;

    public SplitDetailsPage(
        ExpenseSplit split,
        ISplitService splitService,
        IPaymentRequestService paymentRequestService,
        IPaymentGatewayService paymentGatewayService)
    {
        InitializeComponent();
        _split = split;
        _splitService = splitService;
        _paymentRequestService = paymentRequestService;
        _paymentGatewayService = paymentGatewayService;
        BindingContext = _split;
    }

    private async void OnTogglePaidClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: SplitParticipant participant }) return;

        participant.IsPaid = !participant.IsPaid;
        participant.PaidAt = participant.IsPaid ? DateTime.UtcNow : null;
        await _splitService.UpdateParticipantAsync(participant);
        RefreshBindings();
    }

    private async void OnCardRequestClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: SplitParticipant participant }) return;

        try
        {
            var request = await _paymentGatewayService.CreateCardRequestAsync(_split, participant);
            if (string.IsNullOrWhiteSpace(request.CheckoutUrl))
                throw new InvalidOperationException("The payment provider did not return a checkout URL.");

            await _paymentRequestService.ShareCardRequestAsync(_split, participant, request.CheckoutUrl);
            participant.PaymentProvider = "stripe";
            participant.ExternalPaymentId = request.Id;
            participant.LastPaymentRequestAt = DateTime.UtcNow;
            await _splitService.UpdateParticipantAsync(participant);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Card Payment Request", ex.Message, "OK");
        }
    }

    private async void OnInteracRequestClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: SplitParticipant participant }) return;

        try
        {
            var request = await _paymentGatewayService.CreateInteracRequestAsync(_split, participant);
            participant.PaymentProvider = "interac";
            participant.ExternalPaymentId = request.Id;
            participant.LastPaymentRequestAt = DateTime.UtcNow;
            await _splitService.UpdateParticipantAsync(participant);
            await _paymentRequestService.OpenInteracHandoffAsync(_split, participant);
            await DisplayAlert(
                "Interac Request Money",
                "The request details were copied. Complete the Request Money action in your participating bank app, then return here.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Interac Request", ex.Message, "OK");
        }
    }

    private async void OnRefreshPaymentsClicked(object sender, EventArgs e)
    {
        var paidCount = 0;
        try
        {
            foreach (var participant in _split.Participants.Where(participant =>
                         participant.PaymentProvider == "stripe"
                         && Guid.TryParse(participant.ExternalPaymentId, out _)))
            {
                var request = await _paymentGatewayService.GetRequestAsync(participant.ExternalPaymentId);
                if (request?.Status == "paid" && !participant.IsPaid)
                {
                    participant.IsPaid = true;
                    participant.PaidAt = request.PaidAt?.UtcDateTime ?? DateTime.UtcNow;
                    await _splitService.UpdateParticipantAsync(participant);
                    paidCount++;
                }
                else if (request?.Status == "refunded" && participant.IsPaid)
                {
                    participant.IsPaid = false;
                    participant.PaidAt = null;
                    await _splitService.UpdateParticipantAsync(participant);
                }
            }

            RefreshBindings();
            await DisplayAlert(
                "Payment Status",
                paidCount > 0 ? $"Confirmed {paidCount} new card payment(s)." : "Payment status is up to date.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Payment Status", ex.Message, "OK");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Delete Split",
            $"Delete the split for {_split.Title}? The original transaction will remain.",
            "Delete",
            "Cancel");
        if (!confirmed) return;

        await _splitService.DeleteSplitAsync(_split.SyncId);
        await Navigation.PopAsync();
    }

    private void RefreshBindings()
    {
        BindingContext = null;
        BindingContext = _split;
    }
}
