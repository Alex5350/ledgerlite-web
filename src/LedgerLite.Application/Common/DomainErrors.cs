using ErrorOr;

namespace LedgerLite.Application.Common;

/// <summary>
/// Canonical <see cref="Error"/> catalog for every business-rule violation in the domain.
/// Handlers return these instead of throwing.
/// </summary>
public static class DomainErrors
{
    public static class Users
    {
        public static Error EmailAlreadyInUse => Error.Conflict(
            code: "Users.EmailAlreadyInUse",
            description: "A user with this email address already exists.");

        public static Error NotFound => Error.NotFound(
            code: "Users.NotFound",
            description: "User was not found.");
    }

    public static class FiscalPeriods
    {
        public static Error NotFound => Error.NotFound(
            code: "FiscalPeriods.NotFound",
            description: "Fiscal period was not found.");

        public static Error AlreadyClosed => Error.Conflict(
            code: "FiscalPeriods.AlreadyClosed",
            description: "Fiscal period is already closed.");

        public static Error CloseBeforeEndDate => Error.Conflict(
            code: "FiscalPeriods.CloseBeforeEndDate",
            description: "Fiscal period cannot be closed before its end date.");

        public static Error ClosedForPosting => Error.Conflict(
            code: "FiscalPeriods.ClosedForPosting",
            description: "Cannot post journal entries to a closed fiscal period.");

        public static Error EndDateBeforeStartDate => Error.Validation(
            code: "FiscalPeriods.EndDateBeforeStartDate",
            description: "Fiscal period end date must not be before its start date.");

        public static Error NameRequired => Error.Validation(
            code: "FiscalPeriods.NameRequired",
            description: "Fiscal period name is required.");
    }

    public static class Accounts
    {
        public static Error NotFound => Error.NotFound(
            code: "Accounts.NotFound",
            description: "Account was not found.");

        public static Error NumberTaken => Error.Conflict(
            code: "Accounts.NumberTaken",
            description: "An account with this number already exists in the fiscal period.");

        public static Error InvalidNumber => Error.Validation(
            code: "Accounts.InvalidNumber",
            description: "Account number must be a 4-digit string between '1000' and '9999'.");

        public static Error InvalidType => Error.Validation(
            code: "Accounts.InvalidType",
            description: "Account type must be one of: Asset, Liability, Equity, Revenue, Expense.");

        public static Error NameRequired => Error.Validation(
            code: "Accounts.NameRequired",
            description: "Account name is required.");
    }

    public static class JournalEntries
    {
        public static Error NotFound => Error.NotFound(
            code: "JournalEntries.NotFound",
            description: "Journal entry was not found.");

        public static Error TooFewLines => Error.Validation(
            code: "JournalEntries.TooFewLines",
            description: "A journal entry must have at least two lines.");

        public static Error LineMustHaveExactlyOneSide => Error.Validation(
            code: "JournalEntries.LineMustHaveExactlyOneSide",
            description: "Each journal entry line must have exactly one positive side (debit or credit).");

        public static Error NegativeAmount => Error.Validation(
            code: "JournalEntries.NegativeAmount",
            description: "Journal entry line amounts cannot be negative.");

        public static Error NotBalanced(decimal debits, decimal credits) => Error.Validation(
            code: "JournalEntries.NotBalanced",
            description: $"Journal entry is not balanced: debits {debits} != credits {credits}.");

        public static Error AccountNotFound(Guid accountId) => Error.NotFound(
            code: "JournalEntries.AccountNotFound",
            description: $"Account '{accountId}' referenced by a journal entry line was not found.");

        public static Error AlreadyPosted => Error.Conflict(
            code: "JournalEntries.AlreadyPosted",
            description: "Journal entry has already been posted.");
    }

    public static class Budgets
    {
        public static Error NotFound => Error.NotFound(
            code: "Budgets.NotFound",
            description: "Budget was not found.");

        public static Error CategoryRequired => Error.Validation(
            code: "Budgets.CategoryRequired",
            description: "Budget category is required.");

        public static Error LimitMustBePositive => Error.Validation(
            code: "Budgets.LimitMustBePositive",
            description: "Budget limit must be greater than zero.");

        public static Error AlreadyExistsForCategory => Error.Conflict(
            code: "Budgets.AlreadyExistsForCategory",
            description: "A budget already exists for this category in the fiscal period.");

        public static Error InvalidMoney => Error.Validation(
            code: "Budgets.InvalidMoney",
            description: "Budget limit must be a non-negative amount with at most two decimal places and a 3-letter currency code.");
    }
}
