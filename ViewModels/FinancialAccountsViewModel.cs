using System.Collections.ObjectModel;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.ViewModels;

public class FinancialAccountsViewModel : BaseViewModel
{
    private readonly IFinancialAccountService _accountService;

    public ObservableCollection<FinancialAccount> Accounts { get; } = new();

    public FinancialAccountsViewModel(IFinancialAccountService accountService)
    {
        _accountService = accountService;
    }

    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;

        try
        {
            Accounts.Clear();
            var accounts = await _accountService.GetAccountsAsync();
            foreach (var account in accounts)
                Accounts.Add(account);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveAccountAsync(FinancialAccount account)
    {
        await _accountService.AddOrUpdateAccountAsync(account);
        await LoadAsync();
    }

    public async Task DeleteAccountAsync(FinancialAccount account)
    {
        await _accountService.DeleteAccountAsync(account.Id);
        await LoadAsync();
    }
}
