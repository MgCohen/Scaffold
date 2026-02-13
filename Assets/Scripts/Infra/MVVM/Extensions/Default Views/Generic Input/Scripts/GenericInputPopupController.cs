using Scaffold.MVVM;
using System;
using System.Text.RegularExpressions;

public class GenericInputPopupController : ViewModel
{
    public GenericInputPopupController(string title, string description, Action<string> onConfirm, string placeholder = null, string extraDescription = null, int maxLength = 10, int minLength = 3)
    {
        Title = title;
        Description = description;
        OnConfirm = onConfirm;
        Placeholder = placeholder;
        ExtraDescription = extraDescription;
        this.MaxLength = maxLength;
        this.MinLength = minLength;
    }
    public int MinLength { get; set; }

    public int MaxLength { get; set; }

    public string Title { get; internal set; }
    public string Description { get; internal set; }
    public Action<string> OnConfirm { get; internal set; }
    public string Placeholder { get; internal set; }
    public string ExtraDescription { get; internal set; }

    public void TryConfirmInput(string input)
    {
        input = input?.ToLower();
        OnConfirm?.Invoke(input);
        OnConfirm = null;
    }

    public bool ValidateInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        if (!Regex.Match(input, @"^[a-zA-Z0-9]+$").Success)
        {
            return false;
        }

        if (input.Length > MaxLength)
        {
            return false;
        }

        if (input.Length < MinLength)
        {
            return false;
        }

        return true;
    }
}
