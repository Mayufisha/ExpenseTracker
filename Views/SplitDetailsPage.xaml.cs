using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Views;

public partial class SplitDetailsPage : ContentPage
{
    private readonly ExpenseSplit _split;
    private readonly ISplitService _splitService;
    private readonly IPaymentRequestService _paymentRequestService;

    public SplitDetailsPage(
        ExpenseSplit split,
        ISplitService splitService,
        IPaymentRequestService paymentRequestService)
    {
        InitializeComponent();
        _split = split;
        _splitService = splitService;
        _paymentRequestService = paymentRequestService;
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

    private async void OnRequestPaymentClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: SplitParticipant participant }) return;

        await _paymentRequestService.ShareRequestAsync(_split, participant);
        participant.LastPaymentRequestAt = DateTime.UtcNow;
        await _splitService.UpdateParticipantAsync(participant);
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
