using FluentValidation;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace PredictionLeague.Web.Client.Components.Shared;

/// <summary>
/// Integrates FluentValidation validators with Blazor's EditForm validation system.
/// Replaces Blazored.FluentValidation with a simple, maintainable implementation.
/// </summary>
public class FluentValidationValidator : ComponentBase, IDisposable
{
    [CascadingParameter]
    private EditContext? EditContext { get; set; }

    [Inject]
    private IServiceProvider ServiceProvider { get; init; } = default!;

    private ValidationMessageStore? _messageStore;
    private IValidator? _validator;

    protected override void OnInitialized()
    {
        if (EditContext is null)
            throw new InvalidOperationException(
                $"{nameof(FluentValidationValidator)} requires a cascading parameter of type {nameof(EditContext)}. " +
                $"Ensure this component is used inside an EditForm.");

        _messageStore = new ValidationMessageStore(EditContext);
        _validator = GetValidatorForModel(EditContext.Model);

        EditContext.OnValidationRequested += HandleValidationRequested;
        EditContext.OnFieldChanged += HandleFieldChanged;
    }

    private IValidator? GetValidatorForModel(object model)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(model.GetType());
        return ServiceProvider.GetService(validatorType) as IValidator;
    }

    private async void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        if (EditContext is null || _messageStore is null || _validator is null)
            return;

        _messageStore.Clear();

        var context = new ValidationContext<object>(EditContext.Model);
        var result = await _validator.ValidateAsync(context);

        foreach (var error in result.Errors)
        {
            var fieldIdentifier = new FieldIdentifier(EditContext.Model, error.PropertyName);
            _messageStore.Add(fieldIdentifier, error.ErrorMessage);
        }

        EditContext.NotifyValidationStateChanged();
    }

    private async void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        if (EditContext is null || _messageStore is null || _validator is null)
            return;

        // Clear previous errors for this field
        _messageStore.Clear(e.FieldIdentifier);

        // Validate just this field using FluentValidation's property selector
        var context = new ValidationContext<object>(
            EditContext.Model,
            new PropertyChain(),
            new MemberNameValidatorSelector(new[] { e.FieldIdentifier.FieldName }));

        var result = await _validator.ValidateAsync(context);

        foreach (var error in result.Errors.Where(err => err.PropertyName == e.FieldIdentifier.FieldName))
        {
            _messageStore.Add(e.FieldIdentifier, error.ErrorMessage);
        }

        EditContext.NotifyValidationStateChanged();
    }

    public void Dispose()
    {
        if (EditContext is not null)
        {
            EditContext.OnValidationRequested -= HandleValidationRequested;
            EditContext.OnFieldChanged -= HandleFieldChanged;
        }
    }
}
